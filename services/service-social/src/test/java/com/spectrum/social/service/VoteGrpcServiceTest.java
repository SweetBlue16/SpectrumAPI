package com.spectrum.social.service;

import com.spectrum.social.grpc.GetUserVotesRequest;
import com.spectrum.social.grpc.GetUserVotesResponse;
import com.spectrum.social.model.Vote;
import io.grpc.stub.StreamObserver;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.Mockito.mock;
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

    private static class CapturingObserver<T> implements StreamObserver<T> {
        private T value;
        private boolean completed;

        @Override
        public void onNext(T value) {
            this.value = value;
        }

        @Override
        public void onError(Throwable throwable) {
            throw new AssertionError(throwable);
        }

        @Override
        public void onCompleted() {
            completed = true;
        }
    }
}
