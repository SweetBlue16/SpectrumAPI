package com.spectrum.social.service;

import com.spectrum.social.grpc.CommentServiceGrpc;
import com.spectrum.social.grpc.PublishCommentRequest;
import com.spectrum.social.grpc.CommentResponse;
import com.spectrum.social.grpc.GetCommentsRequest;
import com.spectrum.social.grpc.GetCommentCountsRequest;
import com.spectrum.social.grpc.CommentCount;
import com.spectrum.social.grpc.CommentCountsResponse;
import com.spectrum.social.grpc.DeleteCommentRequest;
import com.spectrum.social.grpc.DeleteResponse;
import com.spectrum.social.model.Comment;
import com.spectrum.social.repository.CommentRepository;
import io.grpc.Status;
import io.grpc.stub.StreamObserver;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import net.devh.boot.grpc.server.service.GrpcService;
import org.springframework.data.domain.PageRequest;
import org.springframework.data.domain.Sort;
import org.springframework.data.mongodb.core.MongoTemplate;
import org.springframework.data.mongodb.core.aggregation.Aggregation;
import org.springframework.data.mongodb.core.query.Criteria;

import java.time.Instant;
import java.util.List;

@GrpcService
@RequiredArgsConstructor
@Slf4j
public class CommentGrpcService extends CommentServiceGrpc.CommentServiceImplBase {

    private static final int PAGE_SIZE = 20;
    private static final int MAX_CONTENT_LENGTH = 500;
    private static final String ADMIN_ROLE = "ADMIN";
    private static final String REVIEW_ID_FIELD = "reviewId";

    private final CommentRepository commentRepository;
    private final MongoTemplate mongoTemplate;

    @Override
    public void publishComment(PublishCommentRequest request, StreamObserver<CommentResponse> responseObserver) {
        if (hasMissingPublishFields(request)) {
            responseObserver.onError(Status.INVALID_ARGUMENT
                    .withDescription("userId, reviewId and content are required.")
                    .asRuntimeException());
            return;
        }

        String content = request.getContent().trim();
        if (content.length() > MAX_CONTENT_LENGTH) {
            responseObserver.onError(Status.INVALID_ARGUMENT
                    .withDescription("El comentario no puede superar los 500 caracteres.")
                    .asRuntimeException());
            return;
        }

        Comment comment = Comment.builder()
                .userId(request.getUserId())
                .reviewId(request.getReviewId())
                .gameId(request.getGameId())
                .content(content)
                .publishedAt(Instant.now())
                .build();

        Comment savedComment = commentRepository.save(comment);

        log.info("Published comment {} from user {} on review {}", savedComment.getId(), request.getUserId(), request.getReviewId());
        responseObserver.onNext(toResponse(savedComment));
        responseObserver.onCompleted();
    }

    @Override
    public void getCommentsByReview(GetCommentsRequest request, StreamObserver<CommentResponse> responseObserver) {
        if (request.getReviewId().isBlank()) {
            responseObserver.onError(Status.INVALID_ARGUMENT
                    .withDescription("reviewId is required.")
                    .asRuntimeException());
            return;
        }

        int page = Math.max(request.getPage(), 1) - 1;
        PageRequest pageable = PageRequest.of(page, PAGE_SIZE, Sort.by(Sort.Direction.ASC, "publishedAt"));

        commentRepository.findByReviewId(request.getReviewId(), pageable)
                .forEach(comment -> responseObserver.onNext(toResponse(comment)));

        log.info("Fetched comments for review {}, page {}", request.getReviewId(), request.getPage());
        responseObserver.onCompleted();
    }

    @Override
    public void getCommentCounts(GetCommentCountsRequest request, StreamObserver<CommentCountsResponse> responseObserver) {
        if (request.getReviewIdsCount() == 0) {
            responseObserver.onNext(CommentCountsResponse.newBuilder().build());
            responseObserver.onCompleted();
            return;
        }

        Criteria criteria = Criteria.where(REVIEW_ID_FIELD).in(request.getReviewIdsList());
        if (request.getFrom() > 0 || request.getTo() > 0) {
            Criteria publishedAtCriteria = Criteria.where("publishedAt");
            if (request.getFrom() > 0) {
                publishedAtCriteria = publishedAtCriteria.gte(Instant.ofEpochMilli(request.getFrom()));
            }
            if (request.getTo() > 0) {
                publishedAtCriteria = publishedAtCriteria.lt(Instant.ofEpochMilli(request.getTo()));
            }
            criteria = new Criteria().andOperator(criteria, publishedAtCriteria);
        }

        Aggregation aggregation = Aggregation.newAggregation(
                Aggregation.match(criteria),
                Aggregation.group(REVIEW_ID_FIELD).count().as("count"),
                Aggregation.project("count").and("_id").as(REVIEW_ID_FIELD)
        );

        List<CommentCountDocument> documents = mongoTemplate
                .aggregate(aggregation, "comments", CommentCountDocument.class)
                .getMappedResults();

        CommentCountsResponse.Builder response = CommentCountsResponse.newBuilder();
        documents.forEach(document -> response.addCounts(CommentCount.newBuilder()
                .setReviewId(document.getReviewId() == null ? "" : document.getReviewId())
                .setCount(document.getCount())
                .build()));

        responseObserver.onNext(response.build());
        responseObserver.onCompleted();
    }

    @Override
    public void deleteComment(DeleteCommentRequest request, StreamObserver<DeleteResponse> responseObserver) {
        if (request.getCommentId().isBlank() || request.getRequesterId().isBlank()) {
            responseObserver.onError(Status.INVALID_ARGUMENT
                    .withDescription("commentId and requesterId are required.")
                    .asRuntimeException());
            return;
        }

        var optionalComment = commentRepository.findById(request.getCommentId());
        if (optionalComment.isEmpty()) {
            responseObserver.onError(Status.NOT_FOUND
                    .withDescription("El comentario solicitado no existe.")
                    .asRuntimeException());
            return;
        }

        Comment comment = optionalComment.get();
        boolean isOwner = comment.getUserId().equals(request.getRequesterId());
        boolean isAdmin = ADMIN_ROLE.equals(request.getRequesterRole());

        if (!isOwner && !isAdmin) {
            responseObserver.onError(Status.PERMISSION_DENIED
                    .withDescription("No tienes permisos para eliminar este comentario.")
                    .asRuntimeException());
            return;
        }

        commentRepository.delete(comment);
        log.info("Deleted comment {} requested by {}", request.getCommentId(), request.getRequesterId());
        responseObserver.onNext(DeleteResponse.newBuilder().setSuccess(true).build());
        responseObserver.onCompleted();
    }

    private static boolean hasMissingPublishFields(PublishCommentRequest request) {
        return request.getUserId().isBlank()
                || request.getReviewId().isBlank()
                || request.getContent().isBlank();
    }

    private static CommentResponse toResponse(Comment comment) {
        return CommentResponse.newBuilder()
                .setCommentId(comment.getId() == null ? "" : comment.getId())
                .setUserId(comment.getUserId())
                .setReviewId(comment.getReviewId())
                .setContent(comment.getContent())
                .setPublishedAt(comment.getPublishedAt() == null ? 0 : comment.getPublishedAt().toEpochMilli())
                .setGameId(comment.getGameId() == null ? "" : comment.getGameId())
                .build();
    }

    static class CommentCountDocument {
        private String reviewId;
        private int count;

        public String getReviewId() {
            return reviewId;
        }

        public void setReviewId(String reviewId) {
            this.reviewId = reviewId;
        }

        public int getCount() {
            return count;
        }

        public void setCount(int count) {
            this.count = count;
        }
    }
}
