package com.spectrum.social.service;

import com.spectrum.social.grpc.ReportServiceGrpc;
import com.spectrum.social.grpc.SubmitReportRequest;
import com.spectrum.social.grpc.ReportResponse;
import com.spectrum.social.grpc.ListReportsRequest;
import com.spectrum.social.grpc.ReportDetails;
import com.spectrum.social.grpc.UpdateReportStatusRequest;
import com.spectrum.social.grpc.ReportActionResponse;
import com.spectrum.social.model.Report;
import com.spectrum.social.repository.ReportRepository;
import io.grpc.Status;
import io.grpc.stub.StreamObserver;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import net.devh.boot.grpc.server.service.GrpcService;

import java.time.Instant;

@GrpcService
@RequiredArgsConstructor
@Slf4j
public class ReportGrpcService extends ReportServiceGrpc.ReportServiceImplBase {

    private final ReportRepository reportRepository;

    @Override
    public void submitReport(SubmitReportRequest request, StreamObserver<ReportResponse> responseObserver) {
        Report report = Report.builder()
                .reporterId(request.getReporterId())
                .targetId(request.getTargetId())
                .targetType(request.getTargetType())
                .reason(request.getReason())
                .status("PENDIENTE")
                .reportedAt(Instant.now())
                .build();

        reportRepository.save(report);

        responseObserver.onNext(ReportResponse.newBuilder().setSuccess(true).build());
        responseObserver.onCompleted();
    }

    @Override
    public void listReportsByStatus(ListReportsRequest request, StreamObserver<ReportDetails> responseObserver) {
        reportRepository.findByStatus(request.getStatus()).forEach(r -> {
            responseObserver.onNext(ReportDetails.newBuilder()
                    .setReportId(r.getId())
                    .setStatus(r.getStatus())
                    .setReason(r.getReason())
                    .setTargetType(r.getTargetType())
                    .setReportedAt(r.getReportedAt().getEpochSecond())
                    .build());
        });
        responseObserver.onCompleted();
    }

    @Override
    public void updateReportStatus(UpdateReportStatusRequest request, StreamObserver<ReportActionResponse> responseObserver) {
        reportRepository.findById(request.getReportId()).ifPresentOrElse(report -> {
            report.setStatus(request.getNewStatus());
            report.setModeratorId(request.getModeratorId());
            report.setResolutionNotes(request.getResolutionNotes());
            report.setResolvedAt(Instant.now());

            reportRepository.save(report);

            responseObserver.onNext(ReportActionResponse.newBuilder()
                    .setSuccess(true)
                    .setMessage("Reporte actualizado a " + request.getNewStatus())
                    .build());
        }, () -> {
            responseObserver.onError(Status.NOT_FOUND.withDescription("Reporte no encontrado").asRuntimeException());
        });
        responseObserver.onCompleted();
    }
}
