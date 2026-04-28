package com.spectrum.drops.service;

import com.spectrum.drops.grpc.ClaimKeyRequest;
import com.spectrum.drops.grpc.ClaimKeyResponse;
import com.spectrum.drops.grpc.DropServiceGrpc;
import com.spectrum.drops.grpc.EventStatusResponse;
import com.spectrum.drops.grpc.GetEventRequest;
import com.spectrum.drops.grpc.WonKey;
import com.spectrum.drops.grpc.WonKeysRequest;
import com.spectrum.drops.grpc.WonKeysResponse;
import com.spectrum.drops.repository.AccessKeyRepository;
import com.spectrum.drops.repository.EventRepository;
import io.grpc.Status;
import io.grpc.stub.StreamObserver;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import net.devh.boot.grpc.server.service.GrpcService;

import java.util.stream.Collectors;

@GrpcService
@RequiredArgsConstructor
@Slf4j
public class DropsGrpcService extends DropServiceGrpc.DropServiceImplBase {

    private final AccessKeyRepository accessKeyRepository;
    private final EventRepository eventRepository;

    @Override
    public void getEventStatus(GetEventRequest request, StreamObserver<EventStatusResponse> responseObserver) {
        log.info("Consultando estado del evento: {}", request.getEventId());

        eventRepository.findById(request.getEventId()).ifPresentOrElse(event -> {
            EventStatusResponse response = EventStatusResponse.newBuilder()
                    .setEventId(event.getId())
                    .setKeysAvailable(event.getKeysAvailable())
                    .setKeysTotal(event.getKeysTotal())
                    .setStatus(event.getStatus())
                    .setEndDate(event.getEndDate().getEpochSecond())
                    .build();

            responseObserver.onNext(response);
            responseObserver.onCompleted();
        }, () -> {
            responseObserver.onError(Status.NOT_FOUND
                    .withDescription("El evento solicitado no existe")
                    .asRuntimeException());
        });
    }

    @Override
    public void claimAccessKey(ClaimKeyRequest request, StreamObserver<ClaimKeyResponse> responseObserver) {
        // TODO: Implementar lógica transaccional
        responseObserver.onNext(ClaimKeyResponse.newBuilder().setSuccess(false).build());
        responseObserver.onCompleted();
    }

    @Override
    public void getWonKeys(WonKeysRequest request, StreamObserver<WonKeysResponse> responseObserver) {
        var keys = accessKeyRepository.findByUserId(request.getUserId()).stream()
                .map(k -> WonKey.newBuilder()
                        .setEventId(k.getEventId())
                        .setGameTitle(k.getGameTitle())
                        .setAccessKeyCode(k.getAccessKeyCode())
                        .setClaimedAt(k.getClaimedAt().getEpochSecond())
                        .build())
                .collect(Collectors.toList());

        responseObserver.onNext(WonKeysResponse.newBuilder().addAllWonKeys(keys).build());
        responseObserver.onCompleted();
    }
}
