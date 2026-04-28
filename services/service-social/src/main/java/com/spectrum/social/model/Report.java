package com.spectrum.social.model;

import lombok.Builder;
import lombok.Data;
import org.springframework.data.annotation.Id;
import org.springframework.data.mongodb.core.mapping.Document;

import java.time.Instant;

@Data
@Builder
@Document(collection = "reports")
public class Report {
    @Id
    private String id;
    private String reporterId;
    private String targetId;
    private String targetType;
    private String reason;
    private String status;
    private Instant reportedAt;
    private String moderatorId;
    private String resolutionNotes;
    private Instant resolvedAt;
}
