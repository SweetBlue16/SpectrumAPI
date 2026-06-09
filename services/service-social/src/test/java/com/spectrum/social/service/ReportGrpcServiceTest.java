package com.spectrum.social.service;

import com.spectrum.social.grpc.*;
import com.spectrum.social.model.Report;
import com.spectrum.social.repository.ReportRepository;
import io.grpc.stub.StreamObserver;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.dao.DataAccessException;

import java.time.Instant;
import java.util.Arrays;
import java.util.List;
import java.util.Optional;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
class ReportGrpcServiceTest {

    private final Instant FIXED_INSTANT = Instant.parse("2026-06-01T12:00:00Z");

    @Mock
    private ReportRepository reportRepository;

    @Mock
    private StreamObserver<ReportResponse> responseObserver;

    @InjectMocks
    private ReportGrpcService reportGrpcService;

    @BeforeEach
    void setUp() {
        reset(reportRepository, responseObserver);
    }

    @Test
    void createReportValidRequestCallsRepositorySave() {
        SubmitReportRequest request = buildValidRequest();
        when(reportRepository.save(any(Report.class))).thenReturn(new Report());

        reportGrpcService.submitReport(request, responseObserver);

        verify(reportRepository, times(1)).save(any(Report.class));
    }

    @Test
    void createReportValidRequestSendsSuccessResponse() {
        SubmitReportRequest request = buildValidRequest();
        Report savedReport = new Report();
        savedReport.setId("mock-id-123");
        when(reportRepository.save(any(Report.class))).thenReturn(savedReport);

        ArgumentCaptor<ReportResponse> responseCaptor = ArgumentCaptor.forClass(ReportResponse.class);

        reportGrpcService.submitReport(request, responseObserver);

        verify(responseObserver).onNext(responseCaptor.capture());
        assertTrue(responseCaptor.getValue().getSuccess());
        verify(responseObserver).onCompleted();
    }

    @Test
    void createReportSupportedTargetTypesShouldPassValidation() {
        for (String targetType : List.of("COMMENT", "USER", "GAME_CLIP")) {
            SubmitReportRequest request = SubmitReportRequest.newBuilder()
                    .setReporterId("user-" + targetType)
                    .setTargetId("target-" + targetType)
                    .setTargetType(targetType)
                    .setReason("Policy")
                    .build();
            when(reportRepository.existsByReporterIdAndTargetId(request.getReporterId(), request.getTargetId()))
                    .thenReturn(false);
            when(reportRepository.save(any(Report.class))).thenReturn(new Report());

            reportGrpcService.submitReport(request, responseObserver);
        }

        verify(reportRepository, times(3)).save(any(Report.class));
        verify(responseObserver, times(3)).onNext(any(ReportResponse.class));
        verify(responseObserver, times(3)).onCompleted();
    }

    @Test
    void createReportWhenDuplicateExistsShouldReturnBusinessErrorWithoutSaving() {
        SubmitReportRequest request = buildValidRequest();
        when(reportRepository.existsByReporterIdAndTargetId(request.getReporterId(), request.getTargetId()))
                .thenReturn(true);
        ArgumentCaptor<ReportResponse> responseCaptor = ArgumentCaptor.forClass(ReportResponse.class);

        reportGrpcService.submitReport(request, responseObserver);

        verify(reportRepository, never()).save(any());
        verify(responseObserver).onNext(responseCaptor.capture());
        assertFalse(responseCaptor.getValue().getSuccess());
        assertEquals("You have already reported this content.", responseCaptor.getValue().getMessage());
    }

    @Test
    void createReportDatabaseAccessExceptionSendsDatabaseErrorResponse() {
        SubmitReportRequest request = buildValidRequest();
        when(reportRepository.save(any())).thenThrow(new DataAccessException("db unavailable") { });
        ArgumentCaptor<ReportResponse> responseCaptor = ArgumentCaptor.forClass(ReportResponse.class);

        reportGrpcService.submitReport(request, responseObserver);

        verify(responseObserver).onNext(responseCaptor.capture());
        assertFalse(responseCaptor.getValue().getSuccess());
        assertTrue(responseCaptor.getValue().getMessage().contains("database error"));
        verify(responseObserver).onCompleted();
    }

    @Test
    void createReportEmptyReporterIdSendsErrorResponse() {
        SubmitReportRequest request = SubmitReportRequest.newBuilder()
                .setReporterId("")
                .setTargetId("target-1")
                .build();

        ArgumentCaptor<ReportResponse> responseCaptor = ArgumentCaptor.forClass(ReportResponse.class);

        reportGrpcService.submitReport(request, responseObserver);

        verify(reportRepository, never()).save(any());
        verify(responseObserver).onNext(responseCaptor.capture());
        assertFalse(responseCaptor.getValue().getSuccess());
    }

    @Test
    void createReportDatabaseThrowsExceptionSendsErrorResponse() {
        SubmitReportRequest request = buildValidRequest();
        when(reportRepository.save(any())).thenThrow(new RuntimeException("MongoDB Connection Timeout"));

        ArgumentCaptor<ReportResponse> responseCaptor = ArgumentCaptor.forClass(ReportResponse.class);

        reportGrpcService.submitReport(request, responseObserver);

        verify(responseObserver).onNext(responseCaptor.capture());
        assertFalse(responseCaptor.getValue().getSuccess());
        verify(responseObserver).onCompleted();
    }

    @Test
    void listReportsByStatusValidStatusStreamsReports() {
        ListReportsRequest request = ListReportsRequest.newBuilder()
                .setStatus("PENDING")
                .build();

        Report report1 = new Report();
        report1.setId("report-1");
        report1.setReporterId("user-1");
        report1.setTargetId("target-1");
        report1.setTargetType("REVIEW");
        report1.setReason("Spam");
        report1.setStatus("PENDING");
        report1.setReportedAt(FIXED_INSTANT);

        Report report2 = new Report();
        report2.setId("report-2");
        report2.setReporterId("user-2");
        report2.setTargetId("target-2");
        report2.setTargetType("COMMENT");
        report2.setReason("Harassment");
        report2.setStatus("PENDING");
        report2.setReportedAt(FIXED_INSTANT);
        report2.setDescription("Details");
        report2.setModeratorId("admin-1");
        report2.setResolutionNotes("Handled");
        report2.setResolvedAt(FIXED_INSTANT.toEpochMilli());

        List<Report> mockReports = Arrays.asList(report1, report2);
        when(reportRepository.findByStatus("PENDING")).thenReturn(mockReports);

        StreamObserver<ReportDetails> listResponseObserver = mock(StreamObserver.class);
        ArgumentCaptor<ReportDetails> detailsCaptor = ArgumentCaptor.forClass(ReportDetails.class);

        reportGrpcService.listReportsByStatus(request, listResponseObserver);

        verify(listResponseObserver, times(2)).onNext(detailsCaptor.capture());
        assertEquals("", detailsCaptor.getAllValues().get(0).getDescription());
        assertEquals("Details", detailsCaptor.getAllValues().get(1).getDescription());
        assertEquals("admin-1", detailsCaptor.getAllValues().get(1).getModeratorId());
        assertEquals("Handled", detailsCaptor.getAllValues().get(1).getResolutionNotes());
        verify(listResponseObserver, times(1)).onCompleted();
    }

    @Test
    void listReportsByStatusEmptyStatusCompletesWithoutStreaming() {
        ListReportsRequest request = ListReportsRequest.newBuilder()
                .setStatus("")
                .build();

        StreamObserver<ReportDetails> listResponseObserver = mock(StreamObserver.class);

        reportGrpcService.listReportsByStatus(request, listResponseObserver);

        verify(reportRepository, never()).findByStatus(anyString());
        verify(listResponseObserver, never()).onNext(any());
        verify(listResponseObserver, times(1)).onCompleted();
    }

    @Test
    void listReportsByStatusDatabaseErrorCompletesSafely() {
        ListReportsRequest request = ListReportsRequest.newBuilder()
                .setStatus("PENDING")
                .build();

        when(reportRepository.findByStatus(anyString())).thenThrow(new RuntimeException("DB Down"));
        StreamObserver<ReportDetails> listResponseObserver = mock(StreamObserver.class);

        reportGrpcService.listReportsByStatus(request, listResponseObserver);

        verify(listResponseObserver, never()).onNext(any());
        verify(listResponseObserver, times(1)).onCompleted();
    }

    @Test
    void updateReportStatusValidRequestUpdatesAndReturnsSuccess() {
        UpdateReportStatusRequest request = UpdateReportStatusRequest.newBuilder()
                .setReportId("report-123")
                .setNewStatus("RESOLVED")
                .setModeratorId("admin-1")
                .setResolutionNotes("User warned")
                .build();

        Report existingReport = new Report();
        existingReport.setId("report-123");
        existingReport.setStatus("PENDING");

        when(reportRepository.findById("report-123")).thenReturn(Optional.of(existingReport));

        StreamObserver<ReportActionResponse> updateResponseObserver = mock(StreamObserver.class);
        ArgumentCaptor<ReportActionResponse> responseCaptor = ArgumentCaptor.forClass(ReportActionResponse.class);

        reportGrpcService.updateReportStatus(request, updateResponseObserver);

        ArgumentCaptor<Report> reportCaptor = ArgumentCaptor.forClass(Report.class);
        verify(reportRepository).save(reportCaptor.capture());

        Report savedReport = reportCaptor.getValue();
        assertEquals("RESOLVED", savedReport.getStatus());
        assertEquals("admin-1", savedReport.getModeratorId());
        assertEquals("User warned", savedReport.getResolutionNotes());

        verify(updateResponseObserver).onNext(responseCaptor.capture());
        assertTrue(responseCaptor.getValue().getSuccess());
        verify(updateResponseObserver).onCompleted();
    }

    @Test
    void updateReportStatusDismissedRequestUpdatesAndReturnsSuccess() {
        UpdateReportStatusRequest request = UpdateReportStatusRequest.newBuilder()
                .setReportId("report-456")
                .setNewStatus("DISMISSED")
                .setModeratorId("admin-2")
                .build();

        Report existingReport = new Report();
        existingReport.setId("report-456");
        existingReport.setStatus("PENDING");
        when(reportRepository.findById("report-456")).thenReturn(Optional.of(existingReport));

        StreamObserver<ReportActionResponse> updateResponseObserver = mock(StreamObserver.class);

        reportGrpcService.updateReportStatus(request, updateResponseObserver);

        verify(reportRepository).save(argThat(report -> "DISMISSED".equals(report.getStatus())));
        verify(updateResponseObserver).onNext(argThat(ReportActionResponse::getSuccess));
        verify(updateResponseObserver).onCompleted();
    }

    @Test
    void updateReportStatusWhenRequiredFieldsAreMissingShouldReturnValidationError() {
        StreamObserver<ReportActionResponse> updateResponseObserver = mock(StreamObserver.class);

        reportGrpcService.updateReportStatus(UpdateReportStatusRequest.newBuilder()
                .setNewStatus("RESOLVED")
                .setModeratorId("admin-1")
                .build(), updateResponseObserver);
        reportGrpcService.updateReportStatus(UpdateReportStatusRequest.newBuilder()
                .setReportId("report-1")
                .setModeratorId("admin-1")
                .build(), updateResponseObserver);
        reportGrpcService.updateReportStatus(UpdateReportStatusRequest.newBuilder()
                .setReportId("report-1")
                .setNewStatus("RESOLVED")
                .build(), updateResponseObserver);

        verify(reportRepository, never()).findById(anyString());
        verify(updateResponseObserver, times(3)).onNext(argThat(response -> !response.getSuccess()));
        verify(updateResponseObserver, times(3)).onCompleted();
    }

    @Test
    void updateReportStatusDataAccessExceptionReturnsDatabaseError() {
        UpdateReportStatusRequest request = UpdateReportStatusRequest.newBuilder()
                .setReportId("report-789")
                .setNewStatus("RESOLVED")
                .setModeratorId("admin-1")
                .build();
        when(reportRepository.findById("report-789")).thenThrow(new DataAccessException("db down") { });
        StreamObserver<ReportActionResponse> updateResponseObserver = mock(StreamObserver.class);
        ArgumentCaptor<ReportActionResponse> responseCaptor = ArgumentCaptor.forClass(ReportActionResponse.class);

        reportGrpcService.updateReportStatus(request, updateResponseObserver);

        verify(updateResponseObserver).onNext(responseCaptor.capture());
        assertFalse(responseCaptor.getValue().getSuccess());
        assertEquals("A database error occurred while updating the report.", responseCaptor.getValue().getMessage());
        verify(updateResponseObserver).onCompleted();
    }

    @Test
    void updateReportStatusUnexpectedExceptionReturnsInternalError() {
        UpdateReportStatusRequest request = UpdateReportStatusRequest.newBuilder()
                .setReportId("report-999")
                .setNewStatus("RESOLVED")
                .setModeratorId("admin-1")
                .build();
        when(reportRepository.findById("report-999")).thenThrow(new RuntimeException("boom"));
        StreamObserver<ReportActionResponse> updateResponseObserver = mock(StreamObserver.class);
        ArgumentCaptor<ReportActionResponse> responseCaptor = ArgumentCaptor.forClass(ReportActionResponse.class);

        reportGrpcService.updateReportStatus(request, updateResponseObserver);

        verify(updateResponseObserver).onNext(responseCaptor.capture());
        assertFalse(responseCaptor.getValue().getSuccess());
        assertEquals("An internal error occurred.", responseCaptor.getValue().getMessage());
        verify(updateResponseObserver).onCompleted();
    }

    @Test
    void updateReportStatusReportNotFoundReturnsError() {
        UpdateReportStatusRequest request = UpdateReportStatusRequest.newBuilder()
                .setReportId("unknown-report")
                .setNewStatus("DISMISSED")
                .setModeratorId("admin-1")
                .build();

        when(reportRepository.findById("unknown-report")).thenReturn(Optional.empty());

        StreamObserver<ReportActionResponse> updateResponseObserver = mock(StreamObserver.class);
        ArgumentCaptor<ReportActionResponse> responseCaptor = ArgumentCaptor.forClass(ReportActionResponse.class);

        reportGrpcService.updateReportStatus(request, updateResponseObserver);

        verify(reportRepository, never()).save(any());

        verify(updateResponseObserver).onNext(responseCaptor.capture());
        assertFalse(responseCaptor.getValue().getSuccess());
        assertEquals("Report not found.", responseCaptor.getValue().getMessage());
        verify(updateResponseObserver).onCompleted();
    }

    @Test
    void updateReportStatusInvalidStatusReturnsError() {
        UpdateReportStatusRequest request = UpdateReportStatusRequest.newBuilder()
                .setReportId("report-123")
                .setNewStatus("INVALID_STATUS")
                .setModeratorId("admin-1")
                .build();

        StreamObserver<ReportActionResponse> updateResponseObserver = mock(StreamObserver.class);
        ArgumentCaptor<ReportActionResponse> responseCaptor = ArgumentCaptor.forClass(ReportActionResponse.class);

        reportGrpcService.updateReportStatus(request, updateResponseObserver);

        verify(reportRepository, never()).findById(anyString());

        verify(updateResponseObserver).onNext(responseCaptor.capture());
        assertFalse(responseCaptor.getValue().getSuccess());
        assertTrue(responseCaptor.getValue().getMessage().contains("Invalid NewStatus"));
        verify(updateResponseObserver).onCompleted();
    }

    private SubmitReportRequest buildValidRequest() {
        return SubmitReportRequest.newBuilder()
                .setReporterId("user-1")
                .setTargetId("review-1")
                .setTargetType("REVIEW")
                .setReason("Spam")
                .build();
    }
}
