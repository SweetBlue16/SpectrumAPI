package com.spectrum.social.model;

import lombok.Builder;
import lombok.Data;
import org.springframework.data.annotation.Id;
import org.springframework.data.mongodb.core.mapping.Document;

import java.time.Instant;

@Data
@Builder
@Document(collection = "comments")
public class Comment {
    @Id
    private String id;
    private String userId;
    private String reviewId;
    private String content;
    private Instant publishedAt;
}
