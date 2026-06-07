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

        List<Report> mockReports = Arrays.asList(report1, report2);
        when(reportRepository.findByStatus("PENDING")).thenReturn(mockReports);

        StreamObserver<ReportDetails> listResponseObserver = mock(StreamObserver.class);

        reportGrpcService.listReportsByStatus(request, listResponseObserver);

        verify(listResponseObserver, times(2)).onNext(any(ReportDetails.class));
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
