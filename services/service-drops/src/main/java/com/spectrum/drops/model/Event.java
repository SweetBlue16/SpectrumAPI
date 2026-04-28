package com.spectrum.drops.model;

import lombok.Builder;
import lombok.Data;
import org.springframework.data.annotation.Id;
import org.springframework.data.mongodb.core.mapping.Document;

import java.time.Instant;

@Data
@Builder
@Document(collection = "events")
public class Event {
    @Id
    private String id;
    private String gameTitle;
    private int keysTotal;
    private int keysAvailable;
    private String status;
    private Instant startDate;
    private Instant endDate;
}
