package com.spectrum.social.service;

import com.spectrum.social.grpc.CommentServiceGrpc;
import com.spectrum.social.grpc.PublishCommentRequest;
import com.spectrum.social.grpc.CommentResponse;
import com.spectrum.social.grpc.GetCommentsRequest;
import com.spectrum.social.grpc.DeleteCommentRequest;
import com.spectrum.social.grpc.DeleteResponse;
import com.spectrum.social.repository.CommentRepository;
import io.grpc.stub.StreamObserver;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import net.devh.boot.grpc.server.service.GrpcService;

@GrpcService
@RequiredArgsConstructor
@Slf4j
public class CommentGrpcService extends CommentServiceGrpc.CommentServiceImplBase {

    private final CommentRepository commentRepository;

    @Override
    public void publishComment(PublishCommentRequest request, StreamObserver<CommentResponse> responseObserver) {
        log.info("Publishing comment from user {} on review {}", request.getUserId(), request.getReviewId());
        responseObserver.onNext(CommentResponse.newBuilder().build());
        responseObserver.onCompleted();
    }

    @Override
    public void getCommentsByReview(GetCommentsRequest request, StreamObserver<CommentResponse> responseObserver) {
        log.info("Fetching comments for review {}, page {}", request.getReviewId(), request.getPage());
        responseObserver.onCompleted();
    }

    @Override
    public void deleteComment(DeleteCommentRequest request, StreamObserver<DeleteResponse> responseObserver) {
        log.info("Deleting comment {} requested by {}", request.getCommentId(), request.getRequesterId());
        responseObserver.onNext(DeleteResponse.newBuilder().setSuccess(true).build());
        responseObserver.onCompleted();
    }
}
