package com.spectrum.social.service;

import com.spectrum.social.grpc.CastVoteRequest;
import com.spectrum.social.grpc.VoteResponse;
import com.spectrum.social.grpc.VoteServiceGrpc;
import com.spectrum.social.repository.VoteRepository;
import io.grpc.stub.StreamObserver;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import net.devh.boot.grpc.server.service.GrpcService;

@GrpcService
@RequiredArgsConstructor
@Slf4j
public class VoteGrpcService extends VoteServiceGrpc.VoteServiceImplBase {

    private final VoteRepository voteRepository;

    @Override
    public void castVote(CastVoteRequest request, StreamObserver<VoteResponse> responseObserver) {
        log.info("Casting {} vote from user {} on {} {}",
                request.getIsPositive() ? "UP" : "DOWN",
                request.getUserId(), request.getTargetType(), request.getTargetId());

        // TODO: Implementar
        responseObserver.onNext(VoteResponse.newBuilder().setSuccess(true).build());
        responseObserver.onCompleted();
    }
}
