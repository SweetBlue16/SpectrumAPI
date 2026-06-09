package com.spectrum.social.service;

import com.spectrum.social.grpc.*;
import com.spectrum.social.model.Vote;
import io.grpc.Status;
import io.grpc.StatusRuntimeException;
import io.grpc.stub.StreamObserver;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.junit.jupiter.api.Assertions.fail;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

class VoteGrpcServiceTest {

    @Test
    void getUserVotesShouldReturnBatchVotesForRequestedReviews() {
        VoteApplicationService applicationService = mock(VoteApplicationService.class);
        VoteGrpcService grpcService = new VoteGrpcService(applicationService);
        when(applicationService.getUserVotes("user-1", "REVIEW", List.of("review-1", "review-2")))
                .thenReturn(List.of(
                        Vote.builder().userId("user-1").targetType("REVIEW").targetId("review-1").positive(true).build(),
                        Vote.builder().userId("user-1").targetType("REVIEW").targetId("review-2").positive(false).build()
                ));

        CapturingObserver<GetUserVotesResponse> observer = new CapturingObserver<>();
        grpcService.getUserVotes(GetUserVotesRequest.newBuilder()
                .setUserId("user-1")
                .setTargetType("REVIEW")
                .addTargetIds("review-1")
                .addTargetIds("review-2")
                .build(), observer);

        assertTrue(observer.completed);
        assertEquals(2, observer.value.getVotesCount());
        assertEquals("review-1", observer.value.getVotes(0).getTargetId());
        assertTrue(observer.value.getVotes(0).getIsPositive());
        assertEquals("review-2", observer.value.getVotes(1).getTargetId());
    }

    @Test
    void castVoteWhenApplicationServiceSucceedsShouldReturnUpdatedCounters() {
        VoteApplicationService applicationService = mock(VoteApplicationService.class);
        VoteGrpcService grpcService = new VoteGrpcService(applicationService);
        when(applicationService.castVote("user-1", "review-1", "REVIEW", true))
                .thenReturn(new VoteApplicationService.VoteCounts(8, 2));

        CapturingObserver<VoteResponse> observer = new CapturingObserver<>();
        grpcService.castVote(CastVoteRequest.newBuilder()
                .setUserId("user-1")
                .setTargetId("review-1")
                .setTargetType("REVIEW")
                .setIsPositive(true)
                .build(), observer);

        assertTrue(observer.completed);
        assertTrue(observer.value.getSuccess());
        assertEquals(8, observer.value.getUpdatedLikes());
        assertEquals(2, observer.value.getUpdatedDislikes());
    }

    @Test
    void castVoteWhenTargetTypeIsUnsupportedShouldReturnInvalidArgument() {
        VoteApplicationService applicationService = mock(VoteApplicationService.class);
        VoteGrpcService grpcService = new VoteGrpcService(applicationService);

        CapturingObserver<VoteResponse> observer = new CapturingObserver<>();
        grpcService.castVote(CastVoteRequest.newBuilder()
                .setUserId("user-1")
                .setTargetId("clip-1")
                .setTargetType("CLIP")
                .build(), observer);

        assertEquals(Status.Code.INVALID_ARGUMENT, observer.error.getStatus().getCode());
    }

    @Test
    void castVoteWhenRequiredFieldsAreMissingShouldReturnInvalidArgumentWithoutCallingApplicationService() {
        VoteApplicationService applicationService = mock(VoteApplicationService.class);
        VoteGrpcService grpcService = new VoteGrpcService(applicationService);

        CapturingObserver<VoteResponse> observer = new CapturingObserver<>();
        grpcService.castVote(CastVoteRequest.newBuilder()
                .setTargetId("review-1")
                .setTargetType("REVIEW")
                .build(), observer);

        assertEquals(Status.Code.INVALID_ARGUMENT, observer.error.getStatus().getCode());
        verify(applicationService, never()).castVote(
                org.mockito.ArgumentMatchers.anyString(),
                org.mockito.ArgumentMatchers.anyString(),
                org.mockito.ArgumentMatchers.anyString(),
                org.mockito.ArgumentMatchers.anyBoolean());
    }

    @Test
    void castVoteWhenApplicationServiceThrowsShouldReturnInternalError() {
        VoteApplicationService applicationService = mock(VoteApplicationService.class);
        VoteGrpcService grpcService = new VoteGrpcService(applicationService);
        when(applicationService.castVote("user-1", "review-1", "REVIEW", false))
                .thenThrow(new RuntimeException("mongo down"));

        CapturingObserver<VoteResponse> observer = new CapturingObserver<>();
        grpcService.castVote(CastVoteRequest.newBuilder()
                .setUserId("user-1")
                .setTargetId("review-1")
                .setTargetType("REVIEW")
                .setIsPositive(false)
                .build(), observer);

        assertEquals(Status.Code.INTERNAL, observer.error.getStatus().getCode());
    }

    @Test
    void getUserVotesWhenRequiredFieldsAreMissingShouldReturnInvalidArgument() {
        VoteGrpcService grpcService = new VoteGrpcService(mock(VoteApplicationService.class));

        CapturingObserver<GetUserVotesResponse> observer = new CapturingObserver<>();
        grpcService.getUserVotes(GetUserVotesRequest.newBuilder()
                .setTargetType("REVIEW")
                .build(), observer);

        assertEquals(Status.Code.INVALID_ARGUMENT, observer.error.getStatus().getCode());
    }

    @Test
    void getUserVotesWhenTargetTypeIsUnsupportedShouldReturnInvalidArgument() {
        VoteGrpcService grpcService = new VoteGrpcService(mock(VoteApplicationService.class));

        CapturingObserver<GetUserVotesResponse> observer = new CapturingObserver<>();
        grpcService.getUserVotes(GetUserVotesRequest.newBuilder()
                .setUserId("user-1")
                .setTargetType("CLIP")
                .addTargetIds("clip-1")
                .build(), observer);

        assertEquals(Status.Code.INVALID_ARGUMENT, observer.error.getStatus().getCode());
    }

    @Test
    void getUserVotesWhenApplicationServiceThrowsShouldReturnInternalError() {
        VoteApplicationService applicationService = mock(VoteApplicationService.class);
        VoteGrpcService grpcService = new VoteGrpcService(applicationService);
        when(applicationService.getUserVotes("user-1", "REVIEW", List.of("review-1")))
                .thenThrow(new RuntimeException("mongo down"));

        CapturingObserver<GetUserVotesResponse> observer = new CapturingObserver<>();
        grpcService.getUserVotes(GetUserVotesRequest.newBuilder()
                .setUserId("user-1")
                .setTargetType("REVIEW")
                .addTargetIds("review-1")
                .build(), observer);

        assertEquals(Status.Code.INTERNAL, observer.error.getStatus().getCode());
    }

    private static class CapturingObserver<T> implements StreamObserver<T> {
        private T value;
        private boolean completed;
        private StatusRuntimeException error;

        @Override
        public void onNext(T value) {
            this.value = value;
        }

        @Override
        public void onError(Throwable throwable) {
            if (throwable instanceof StatusRuntimeException statusRuntimeException) {
                this.error = statusRuntimeException;
                return;
            }
            fail(throwable);
        }

        @Override
        public void onCompleted() {
            completed = true;
        }
    }
}
