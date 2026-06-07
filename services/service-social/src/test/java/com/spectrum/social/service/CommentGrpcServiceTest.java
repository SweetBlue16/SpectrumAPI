package com.spectrum.social.service;

import com.spectrum.social.grpc.*;
import com.spectrum.social.model.Comment;
import com.spectrum.social.repository.CommentRepository;
import io.grpc.Status;
import io.grpc.StatusRuntimeException;
import io.grpc.stub.StreamObserver;
import org.bson.Document;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.data.domain.PageImpl;
import org.springframework.data.domain.Pageable;
import org.springframework.data.mongodb.core.MongoTemplate;
import org.springframework.data.mongodb.core.aggregation.Aggregation;
import org.springframework.data.mongodb.core.aggregation.AggregationResults;

import java.time.Instant;
import java.util.List;
import java.util.Optional;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
class CommentGrpcServiceTest {

    @Mock
    private CommentRepository commentRepository;

    @Mock
    private MongoTemplate mongoTemplate;

    @Mock
    private StreamObserver<CommentCountsResponse> responseObserver;

    private CommentGrpcService commentGrpcService;

    @BeforeEach
    void setUp() {
        commentGrpcService = new CommentGrpcService(commentRepository, mongoTemplate);
    }

    @Test
    void getCommentCountsAggregatesByReviewIdInSingleMongoAggregation() {
        var first = new CommentGrpcService.CommentCountDocument();
        first.setReviewId("review-1");
        first.setCount(4);
        var second = new CommentGrpcService.CommentCountDocument();
        second.setReviewId("review-2");
        second.setCount(2);

        when(mongoTemplate.aggregate(
                any(Aggregation.class),
                eq("comments"),
                eq(CommentGrpcService.CommentCountDocument.class)))
                .thenReturn(new AggregationResults<>(List.of(first, second), new Document()));

        commentGrpcService.getCommentCounts(GetCommentCountsRequest.newBuilder()
                .addReviewIds("review-1")
                .addReviewIds("review-2")
                .setFrom(1000)
                .setTo(2000)
                .build(), responseObserver);

        ArgumentCaptor<CommentCountsResponse> captor = ArgumentCaptor.forClass(CommentCountsResponse.class);
        verify(responseObserver).onNext(captor.capture());
        verify(responseObserver).onCompleted();
        verify(mongoTemplate).aggregate(any(Aggregation.class), eq("comments"), eq(CommentGrpcService.CommentCountDocument.class));

        CommentCountsResponse response = captor.getValue();
        assertEquals(2, response.getCountsCount());
        assertEquals("review-1", response.getCounts(0).getReviewId());
        assertEquals(4, response.getCounts(0).getCount());
        assertEquals("review-2", response.getCounts(1).getReviewId());
        assertEquals(2, response.getCounts(1).getCount());
    }

    @Test
    void getCommentCountsWithoutReviewIdsReturnsEmptyResponseWithoutMongoQuery() {
        commentGrpcService.getCommentCounts(GetCommentCountsRequest.newBuilder().build(), responseObserver);

        ArgumentCaptor<CommentCountsResponse> captor = ArgumentCaptor.forClass(CommentCountsResponse.class);
        verify(responseObserver).onNext(captor.capture());
        verify(responseObserver).onCompleted();
        verify(mongoTemplate, never()).aggregate(any(Aggregation.class), eq("comments"), eq(CommentGrpcService.CommentCountDocument.class));

        assertEquals(0, captor.getValue().getCountsCount());
    }

    @Test
    void publishCommentShouldTrimContentSaveAndReturnCommentPayload() {
        Instant publishedAt = Instant.parse("2026-06-04T18:00:00Z");
        when(commentRepository.save(any(Comment.class))).thenAnswer(invocation -> {
            Comment comment = invocation.getArgument(0);
            comment.setId("comment-1");
            comment.setPublishedAt(publishedAt);
            return comment;
        });
        CapturingObserver<CommentResponse> observer = new CapturingObserver<>();

        commentGrpcService.publishComment(PublishCommentRequest.newBuilder()
                .setUserId("user-1")
                .setReviewId("review-1")
                .setGameId("game-1")
                .setContent("  Great review  ")
                .build(), observer);

        ArgumentCaptor<Comment> commentCaptor = ArgumentCaptor.forClass(Comment.class);
        verify(commentRepository).save(commentCaptor.capture());
        assertEquals("Great review", commentCaptor.getValue().getContent());
        assertEquals("comment-1", observer.value.getCommentId());
        assertEquals("Great review", observer.value.getContent());
        assertEquals(publishedAt.toEpochMilli(), observer.value.getPublishedAt());
        assertTrue(observer.completed);
    }

    @Test
    void publishCommentWhenContentIsTooLongShouldReturnInvalidArgumentWithoutSaving() {
        CapturingObserver<CommentResponse> observer = new CapturingObserver<>();

        commentGrpcService.publishComment(PublishCommentRequest.newBuilder()
                .setUserId("user-1")
                .setReviewId("review-1")
                .setContent("a".repeat(501))
                .build(), observer);

        assertEquals(Status.Code.INVALID_ARGUMENT, observer.error.getStatus().getCode());
        verify(commentRepository, never()).save(any());
    }

    @Test
    void getCommentsByReviewShouldNormalizePageAndStreamRepositoryComments() {
        Instant publishedAt = Instant.parse("2026-06-04T18:00:00Z");
        Comment comment = Comment.builder()
                .id("comment-1")
                .userId("user-1")
                .reviewId("review-1")
                .gameId("game-1")
                .content("Visible comment")
                .publishedAt(publishedAt)
                .build();
        when(commentRepository.findByReviewId(eq("review-1"), any(Pageable.class)))
                .thenReturn(new PageImpl<>(List.of(comment)));
        CapturingObserver<CommentResponse> observer = new CapturingObserver<>();

        commentGrpcService.getCommentsByReview(GetCommentsRequest.newBuilder()
                .setReviewId("review-1")
                .setPage(-5)
                .build(), observer);

        ArgumentCaptor<Pageable> pageableCaptor = ArgumentCaptor.forClass(Pageable.class);
        verify(commentRepository).findByReviewId(eq("review-1"), pageableCaptor.capture());
        assertEquals(0, pageableCaptor.getValue().getPageNumber());
        assertEquals(20, pageableCaptor.getValue().getPageSize());
        assertEquals("comment-1", observer.values.get(0).getCommentId());
        assertEquals("Visible comment", observer.values.get(0).getContent());
        assertTrue(observer.completed);
    }

    @Test
    void deleteCommentWhenRequesterOwnsCommentShouldDeleteAndReturnSuccess() {
        Comment comment = Comment.builder()
                .id("comment-1")
                .userId("owner-1")
                .reviewId("review-1")
                .content("Mine")
                .publishedAt(Instant.parse("2026-06-04T18:00:00Z"))
                .build();
        when(commentRepository.findById("comment-1")).thenReturn(Optional.of(comment));
        CapturingObserver<DeleteResponse> observer = new CapturingObserver<>();

        commentGrpcService.deleteComment(DeleteCommentRequest.newBuilder()
                .setCommentId("comment-1")
                .setRequesterId("owner-1")
                .setRequesterRole("REVIEWER")
                .build(), observer);

        verify(commentRepository).delete(comment);
        assertTrue(observer.value.getSuccess());
        assertTrue(observer.completed);
    }

    @Test
    void deleteCommentWhenRequesterIsNotOwnerOrAdminShouldReturnPermissionDenied() {
        Comment comment = Comment.builder()
                .id("comment-1")
                .userId("owner-1")
                .reviewId("review-1")
                .content("Mine")
                .publishedAt(Instant.parse("2026-06-04T18:00:00Z"))
                .build();
        when(commentRepository.findById("comment-1")).thenReturn(Optional.of(comment));
        CapturingObserver<DeleteResponse> observer = new CapturingObserver<>();

        commentGrpcService.deleteComment(DeleteCommentRequest.newBuilder()
                .setCommentId("comment-1")
                .setRequesterId("other-user")
                .setRequesterRole("REVIEWER")
                .build(), observer);

        assertEquals(Status.Code.PERMISSION_DENIED, observer.error.getStatus().getCode());
        verify(commentRepository, never()).delete(any());
    }

    private static class CapturingObserver<T> implements StreamObserver<T> {
        private T value;
        private final List<T> values = new java.util.ArrayList<>();
        private boolean completed;
        private StatusRuntimeException error;

        @Override
        public void onNext(T value) {
            this.value = value;
            this.values.add(value);
        }

        @Override
        public void onError(Throwable throwable) {
            if (throwable instanceof StatusRuntimeException statusRuntimeException) {
                this.error = statusRuntimeException;
                return;
            }
            throw new AssertionError(throwable);
        }

        @Override
        public void onCompleted() {
            completed = true;
        }
    }
}
