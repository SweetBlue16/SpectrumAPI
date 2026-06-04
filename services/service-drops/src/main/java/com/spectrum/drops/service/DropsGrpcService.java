package com.spectrum.drops.service;

import com.spectrum.drops.grpc.*;
import com.spectrum.drops.model.Event;
import com.spectrum.drops.model.EventParticipant;
import com.spectrum.drops.model.RewardCode;
import com.spectrum.drops.model.Winner;
import com.spectrum.drops.repository.EventParticipantRepository;
import com.spectrum.drops.repository.EventRepository;
import io.grpc.stub.StreamObserver;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import net.devh.boot.grpc.server.service.GrpcService;
import org.springframework.dao.DataAccessException;
import org.springframework.dao.DuplicateKeyException;
import org.springframework.data.domain.Sort;
import org.springframework.data.mongodb.core.FindAndModifyOptions;
import org.springframework.data.mongodb.core.MongoTemplate;
import org.springframework.data.mongodb.core.query.Criteria;
import org.springframework.data.mongodb.core.query.Query;
import org.springframework.data.mongodb.core.query.Update;

import java.time.Instant;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Optional;
import java.util.Set;

@GrpcService
@RequiredArgsConstructor
@Slf4j
public class DropsGrpcService extends DropServiceGrpc.DropServiceImplBase {

    private static final String DRAFT = "DRAFT";
    private static final String SCHEDULED = "SCHEDULED";
    private static final String ACTIVE = "ACTIVE";
    private static final String JOIN_CLOSED = "JOIN_CLOSED";
    private static final String REVEALED = "REVEALED";
    private static final String UPCOMING = "UPCOMING";
    private static final String ACTIVE_JOIN = "ACTIVE_JOIN";
    private static final String REVEAL_ACTIVE = "REVEAL_ACTIVE";
    private static final String EXHAUSTED = "EXHAUSTED";
    private static final String FINISHED = "FINISHED";
    private static final String CANCELLED = "CANCELLED";
    private static final String REWARD_PENDING = "PENDING";
    private static final String REWARD_SENT = "SENT";
    private static final int MAX_SHORT_TEXT_LENGTH = 120;
    private static final int MAX_MEDIUM_TEXT_LENGTH = 300;
    private static final int MAX_URL_LENGTH = 255;
    private static final int MAX_REWARD_CODE_LENGTH = 50;

    private static final String STATUS_FIELD = "status";
    private static final String EVENT_ID_FIELD = "_id";

    private final EventRepository eventRepository;
    private final EventParticipantRepository participantRepository;
    private final MongoTemplate mongoTemplate;

    @Override
    public void createEvent(CreateEventRequest request, StreamObserver<EventActionResponse> responseObserver) {
        try {
            validateEventPayload(
                    request.getTitle(),
                    request.getGameTitle(),
                    request.getPlatform(),
                    request.getStartAt(),
                    request.getJoinDeadlineAt(),
                    request.getRevealAt(),
                    request.getEndAt(),
                    request.getTotalSlots()
            );
            List<RewardCode> rewardCodes = buildRewardCodes(request.getAccessKeysList());

            long now = Instant.now().toEpochMilli();
            Event event = Event.builder()
                    .title(request.getTitle().trim())
                    .description(request.getDescription().trim())
                    .imageUrl(request.getImageUrl().trim())
                    .gameTitle(request.getGameTitle().trim())
                    .rawgGameId(request.getRawgGameId())
                    .platform(request.getPlatform().trim())
                    .startAt(request.getStartAt())
                    .joinDeadlineAt(request.getJoinDeadlineAt())
                    .revealAt(request.getRevealAt())
                    .endAt(request.getEndAt())
                    .totalSlots(request.getTotalSlots())
                    .availableSlots(request.getTotalSlots())
                    .keysTotal(rewardCodes.size())
                    .keysAvailable(rewardCodes.size())
                    .publicChallengeCode("")
                    .createdByAdminId(request.getCreatedByAdminId())
                    .status(request.getPublishNow() ? initialPublishedStatus(request.getStartAt(), now) : DRAFT)
                    .rewardDeliveryStatus(REWARD_PENDING)
                    .participantsCount(0)
                    .rewardCodes(rewardCodes)
                    .winners(new ArrayList<>())
                    .build();

            Event savedEvent = eventRepository.save(event);

            responseObserver.onNext(EventActionResponse.newBuilder()
                    .setSuccess(true)
                    .setMessage("Event created successfully.")
                    .setEventId(savedEvent.getId())
                    .build());
            responseObserver.onCompleted();
        } catch (IllegalArgumentException e) {
            sendActionError(responseObserver, e.getMessage());
        } catch (Exception e) {
            log.error("Error creating giveaway event", e);
            sendActionError(responseObserver, "Failed to create event.");
        }
    }

    @Override
    public void updateEvent(UpdateEventRequest request, StreamObserver<EventActionResponse> responseObserver) {
        try {
            validateEventId(request.getEventId());
            validateEventPayload(
                    request.getTitle(),
                    request.getGameTitle(),
                    request.getPlatform(),
                    request.getStartAt(),
                    request.getJoinDeadlineAt(),
                    request.getRevealAt(),
                    request.getEndAt(),
                    request.getTotalSlots()
            );

            Event event = eventRepository.findById(request.getEventId())
                    .orElseThrow(() -> new IllegalArgumentException("Event not found."));

            if (hasWinners(event)) {
                throw new IllegalStateException("Finished events with a winner cannot be edited.");
            }

            if (event.getParticipantsCount() > request.getTotalSlots()) {
                throw new IllegalArgumentException("Total slots cannot be lower than current participants.");
            }

            event.setTitle(request.getTitle().trim());
            event.setDescription(request.getDescription().trim());
            event.setImageUrl(request.getImageUrl().trim());
            event.setGameTitle(request.getGameTitle().trim());
            event.setRawgGameId(request.getRawgGameId());
            event.setPlatform(request.getPlatform().trim());
            event.setStartAt(request.getStartAt());
            event.setJoinDeadlineAt(request.getJoinDeadlineAt());
            event.setRevealAt(request.getRevealAt());
            event.setEndAt(request.getEndAt());
            event.setTotalSlots(request.getTotalSlots());
            event.setAvailableSlots(request.getTotalSlots() - event.getParticipantsCount());
            event.setPublicChallengeCode("");
            if (request.getAccessKeysCount() > 0) {
                List<RewardCode> rewardCodes = buildRewardCodes(request.getAccessKeysList());
                event.setRewardCodes(rewardCodes);
                event.setKeysTotal(rewardCodes.size());
                event.setKeysAvailable(rewardCodes.size());
            } else if (event.getRewardCodes() != null) {
                event.setKeysTotal(event.getRewardCodes().size());
                event.setKeysAvailable((int) event.getRewardCodes().stream().filter(code -> !code.isClaimed()).count());
            }
            if (!request.getStatus().isBlank()) {
                event.setStatus(request.getStatus());
            }

            eventRepository.save(event);
            sendActionSuccess(responseObserver, event.getId(), "Event updated successfully.");
        } catch (IllegalArgumentException | IllegalStateException e) {
            sendActionError(responseObserver, e.getMessage());
        } catch (Exception e) {
            log.error("Error updating giveaway event {}", request.getEventId(), e);
            sendActionError(responseObserver, "Failed to update event.");
        }
    }

    @Override
    public void publishEvent(PublishEventRequest request, StreamObserver<EventActionResponse> responseObserver) {
        try {
            validateEventId(request.getEventId());
            Event event = eventRepository.findById(request.getEventId())
                    .orElseThrow(() -> new IllegalArgumentException("Event not found."));

            if (FINISHED.equals(event.getStatus()) || CANCELLED.equals(event.getStatus())) {
                throw new IllegalStateException("Finished or cancelled events cannot be published.");
            }

            event.setStatus(initialPublishedStatus(event.getStartAt(), Instant.now().toEpochMilli()));
            eventRepository.save(event);
            sendActionSuccess(responseObserver, event.getId(), "Event published successfully.");
        } catch (IllegalArgumentException | IllegalStateException e) {
            sendActionError(responseObserver, e.getMessage());
        } catch (Exception e) {
            log.error("Error publishing event {}", request.getEventId(), e);
            sendActionError(responseObserver, "Failed to publish event.");
        }
    }

    @Override
    public void finishEvent(FinishEventRequest request, StreamObserver<EventActionResponse> responseObserver) {
        try {
            validateEventId(request.getEventId());
            Event event = eventRepository.findById(request.getEventId())
                    .orElseThrow(() -> new IllegalArgumentException("Event not found."));

            if (FINISHED.equals(event.getStatus()) || EXHAUSTED.equals(event.getStatus()) || CANCELLED.equals(event.getStatus())) {
                sendActionSuccess(responseObserver, event.getId(), "Event already closed.");
                return;
            }

            boolean withoutWinner = !hasWinners(event);
            event.setStatus(withoutWinner && request.getCancelIfWithoutWinner() ? CANCELLED : FINISHED);
            event.setFinishedAt(Instant.now().toEpochMilli());
            eventRepository.save(event);

            sendActionSuccess(responseObserver, event.getId(), "Event closed successfully.");
        } catch (IllegalArgumentException e) {
            sendActionError(responseObserver, e.getMessage());
        } catch (Exception e) {
            log.error("Error finishing event {}", request.getEventId(), e);
            sendActionError(responseObserver, "Failed to finish event.");
        }
    }

    @Override
    public void joinEvent(JoinEventRequest request, StreamObserver<EventActionResponse> responseObserver) {
        EventParticipant insertedParticipant = null;
        try {
            validateEventId(request.getEventId());
            if (request.getUserId().isBlank()) {
                throw new IllegalArgumentException("UserId is required.");
            }

            if (participantRepository.existsByEventIdAndUserId(request.getEventId(), request.getUserId())) {
                throw new IllegalStateException("duplicateParticipation");
            }

            insertedParticipant = EventParticipant.builder()
                    .eventId(request.getEventId())
                    .userId(request.getUserId())
                    .joinedAt(Instant.now().toEpochMilli())
                    .build();
            participantRepository.save(insertedParticipant);

            long now = Instant.now().toEpochMilli();
            Query query = new Query(Criteria.where(EVENT_ID_FIELD).is(request.getEventId())
                    .and(STATUS_FIELD).in(SCHEDULED, ACTIVE, UPCOMING, ACTIVE_JOIN)
                    .and("startAt").lte(now)
                    .and("joinDeadlineAt").gte(now)
                    .and("availableSlots").gt(0));

            Update update = new Update()
                    .inc("availableSlots", -1)
                    .inc("participantsCount", 1);

            Event updated = mongoTemplate.findAndModify(
                    query,
                    update,
                    FindAndModifyOptions.options().returnNew(true),
                    Event.class
            );

            if (updated == null) {
                participantRepository.deleteByEventIdAndUserId(request.getEventId(), request.getUserId());
                throw new IllegalStateException("Event is not accepting participants.");
            }

            sendActionSuccess(responseObserver, updated.getId(), "User joined event.");
        } catch (DuplicateKeyException e) {
            sendActionError(responseObserver, "duplicateParticipation");
        } catch (IllegalArgumentException | IllegalStateException e) {
            rollbackParticipant(insertedParticipant);
            sendActionError(responseObserver, e.getMessage());
        } catch (DataAccessException e) {
            rollbackParticipant(insertedParticipant);
            log.error("Database error joining event {}", request.getEventId(), e);
            sendActionError(responseObserver, "Could not join event.");
        } catch (Exception e) {
            rollbackParticipant(insertedParticipant);
            log.error("Unexpected error joining event {}", request.getEventId(), e);
            sendActionError(responseObserver, "Could not join event.");
        }
    }

    @Override
    public void claimAccessKey(ClaimKeyRequest request, StreamObserver<ClaimKeyResponse> responseObserver) {
        try {
            validateEventId(request.getEventId());
            if (request.getUserId().isBlank() || request.getUsername().isBlank()) {
                throw new IllegalArgumentException("UserId and username are required.");
            }

            if (!participantRepository.existsByEventIdAndUserId(request.getEventId(), request.getUserId())) {
                throw new IllegalStateException("User must join before claiming.");
            }

            long now = Instant.now().toEpochMilli();
            Query query = new Query(Criteria.where(EVENT_ID_FIELD).is(request.getEventId())
                    .and(STATUS_FIELD).nin(DRAFT, CANCELLED, FINISHED, EXHAUSTED)
                    .and("revealAt").lte(now)
                    .and("endAt").gte(now)
                    .and("rewardCodes").elemMatch(Criteria.where("claimed").is(false))
                    .and("rewardCodes.claimedByUserId").ne(request.getUserId())
                    .and("winners.userId").ne(request.getUserId()));

            Update update = new Update()
                    .set("rewardCodes.$.claimed", true)
                    .set("rewardCodes.$.claimedByUserId", request.getUserId())
                    .set("rewardCodes.$.claimedByUsername", request.getUsername())
                    .set("rewardCodes.$.claimedAt", now)
                    .set("rewardCodes.$.deliveryStatus", REWARD_PENDING)
                    .set(STATUS_FIELD, REVEAL_ACTIVE)
                    .inc("keysAvailable", -1);

            Event updated = mongoTemplate.findAndModify(
                    query,
                    update,
                    FindAndModifyOptions.options().returnNew(true),
                    Event.class
            );

            if (updated == null) {
                Optional<Event> event = eventRepository.findById(request.getEventId());
                ClaimKeyResponse.Builder response = ClaimKeyResponse.newBuilder()
                        .setSuccess(false)
                        .setAccessKeyCode("")
                        .setMessage("Challenge could not be claimed.");

                event.ifPresent(value -> {
                    Winner winner = firstWinner(value);
                    if (winner != null) {
                        response.setWinnerUserId(nullToEmpty(winner.getUserId()));
                        response.setWinnerUsername(nullToEmpty(winner.getUsername()));
                        response.setClaimedAt(winner.getClaimedAt() == null ? 0 : winner.getClaimedAt());
                    }
                });

                responseObserver.onNext(response.build());
                responseObserver.onCompleted();
                return;
            }

            RewardCode assignedCode = findAssignedCode(updated, request.getUserId())
                    .or(() -> findAssignedCodeAt(updated, now))
                    .or(() -> findAnyClaimedCode(updated))
                    .orElseThrow(() -> new IllegalStateException("Claimed reward code was not found."));
            Winner winner = Winner.builder()
                    .userId(request.getUserId())
                    .username(request.getUsername())
                    .rewardCode(assignedCode.getCode())
                    .claimedAt(now)
                    .deliveryStatus(REWARD_PENDING)
                    .build();
            boolean exhausted = updated.getKeysAvailable() <= 0;
            Update winnerUpdate = new Update()
                    .push("winners", winner)
                    .set("winnerUserId", request.getUserId())
                    .set("winnerUsername", request.getUsername())
                    .set("rewardDeliveryStatus", REWARD_PENDING);
            if (exhausted) {
                winnerUpdate
                        .set(STATUS_FIELD, EXHAUSTED)
                        .set("finishedAt", now);
            }
            mongoTemplate.updateFirst(
                    new Query(Criteria.where(EVENT_ID_FIELD).is(request.getEventId())),
                    winnerUpdate,
                    Event.class
            );

            responseObserver.onNext(ClaimKeyResponse.newBuilder()
                    .setSuccess(true)
                    .setAccessKeyCode(assignedCode.getCode())
                    .setWinnerUserId(request.getUserId())
                    .setWinnerUsername(request.getUsername())
                    .setClaimedAt(now)
                    .setMessage("Reward code assigned.")
                    .build());
            responseObserver.onCompleted();
        } catch (IllegalArgumentException | IllegalStateException e) {
            log.info("Claim rejected for event {}: {}", request.getEventId(), e.getMessage());
            responseObserver.onNext(ClaimKeyResponse.newBuilder()
                    .setSuccess(false)
                    .setAccessKeyCode("")
                    .setMessage(e.getMessage())
                    .build());
            responseObserver.onCompleted();
        } catch (Exception e) {
            log.error("Unexpected error in claimAccessKey for event {}", request.getEventId(), e);
            responseObserver.onNext(ClaimKeyResponse.newBuilder()
                    .setSuccess(false)
                    .setAccessKeyCode("")
                    .setMessage("Could not claim event.")
                    .build());
            responseObserver.onCompleted();
        }
    }

    @Override
    public void getEventStatus(GetEventRequest request, StreamObserver<EventStatusResponse> responseObserver) {
        try {
            validateEventId(request.getEventId());
            Event event = eventRepository.findById(request.getEventId())
                    .orElseThrow(() -> new IllegalArgumentException("Event not found."));

            responseObserver.onNext(toResponse(event));
            responseObserver.onCompleted();
        } catch (Exception e) {
            log.warn("Error fetching event status {}: {}", request.getEventId(), e.getMessage());
            responseObserver.onNext(EventStatusResponse.newBuilder()
                    .setEventId(request.getEventId())
                    .setStatus("NOT_FOUND")
                    .build());
            responseObserver.onCompleted();
        }
    }

    @Override
    public void listEvents(ListEventsRequest request, StreamObserver<EventListResponse> responseObserver) {
        try {
            int page = Math.max(1, request.getPage());
            int pageSize = Math.min(Math.max(1, request.getPageSize()), 50);
            Query query = new Query(buildScopeCriteria(request.getScope(), request.getIncludeDrafts()));
            long total = mongoTemplate.count(query, Event.class);

            Sort sort = "UPCOMING".equalsIgnoreCase(request.getScope())
                    ? Sort.by(Sort.Direction.ASC, "startAt")
                    : Sort.by(Sort.Direction.DESC, "startAt");

            query.with(sort)
                    .skip((long) (page - 1) * pageSize)
                    .limit(pageSize);

            List<Event> events = mongoTemplate.find(query, Event.class);
            EventListResponse.Builder builder = EventListResponse.newBuilder()
                    .setTotalCount((int) total)
                    .setPage(page)
                    .setPageSize(pageSize);

            events.forEach(event -> builder.addEvents(toResponse(event)));
            responseObserver.onNext(builder.build());
            responseObserver.onCompleted();
        } catch (Exception e) {
            log.error("Error listing giveaway events", e);
            responseObserver.onNext(EventListResponse.newBuilder()
                    .setPage(Math.max(1, request.getPage()))
                    .setPageSize(Math.max(1, request.getPageSize()))
                    .build());
            responseObserver.onCompleted();
        }
    }

    @Override
    public void markRewardSent(MarkRewardSentRequest request, StreamObserver<EventActionResponse> responseObserver) {
        try {
            validateEventId(request.getEventId());
            Query query = new Query(Criteria.where(EVENT_ID_FIELD).is(request.getEventId())
                    .and(STATUS_FIELD).in(FINISHED, EXHAUSTED)
                    .orOperator(
                            Criteria.where("winnerUserId").ne(null),
                            Criteria.where("winners.0").exists(true)
                    ));

            Update update = new Update()
                    .set("rewardSentAt", request.getRewardSentAt())
                    .set("rewardDeliveryStatus", REWARD_SENT);

            Event updated = mongoTemplate.findAndModify(
                    query,
                    update,
                    FindAndModifyOptions.options().returnNew(true),
                    Event.class
            );

            if (updated == null) {
                throw new IllegalStateException("Reward can only be marked sent for finished events with a winner.");
            }

            sendActionSuccess(responseObserver, updated.getId(), "Reward marked as sent.");
        } catch (IllegalArgumentException | IllegalStateException e) {
            sendActionError(responseObserver, e.getMessage());
        } catch (Exception e) {
            log.error("Error marking reward sent for event {}", request.getEventId(), e);
            sendActionError(responseObserver, "Could not mark reward as sent.");
        }
    }

    @Override
    public void getWonKeys(WonKeysRequest request, StreamObserver<WonKeysResponse> responseObserver) {
        try {
            if (request.getUserId().isBlank()) {
                throw new IllegalArgumentException("UserId is required.");
            }

            Query query = new Query(new Criteria().orOperator(
                    Criteria.where("winnerUserId").is(request.getUserId()),
                    Criteria.where("winners.userId").is(request.getUserId())
            )).with(Sort.by(Sort.Direction.DESC, "finishedAt"));
            List<Event> events = mongoTemplate.find(query, Event.class);

            WonKeysResponse.Builder response = WonKeysResponse.newBuilder();
            for (Event event : events) {
                List<Winner> winners = event.getWinners() == null ? List.of() : event.getWinners();
                winners.stream()
                        .filter(winner -> request.getUserId().equals(winner.getUserId()))
                        .forEach(winner -> response.addWonKeys(WonKey.newBuilder()
                                .setEventId(event.getId())
                                .setGameTitle(nullToEmpty(event.getGameTitle()))
                                .setAccessKeyCode(nullToEmpty(winner.getRewardCode()))
                                .setClaimedAt(winner.getClaimedAt() == null ? 0 : winner.getClaimedAt())
                                .setRewardDeliveryStatus(nullToEmpty(winner.getDeliveryStatus()))
                                .build()));
                if ((event.getWinners() == null || event.getWinners().isEmpty())
                        && request.getUserId().equals(event.getWinnerUserId())) {
                    response.addWonKeys(WonKey.newBuilder()
                            .setEventId(event.getId())
                            .setGameTitle(nullToEmpty(event.getGameTitle()))
                            .setAccessKeyCode("")
                            .setClaimedAt(event.getFinishedAt() == null ? 0 : event.getFinishedAt())
                            .setRewardDeliveryStatus(nullToEmpty(event.getRewardDeliveryStatus()))
                            .build());
                }
            }

            responseObserver.onNext(response.build());
            responseObserver.onCompleted();
        } catch (Exception e) {
            log.error("Error fetching won events for user {}", request.getUserId(), e);
            responseObserver.onNext(WonKeysResponse.getDefaultInstance());
            responseObserver.onCompleted();
        }
    }

    private Criteria buildScopeCriteria(String scope, boolean includeDrafts) {
        long now = Instant.now().toEpochMilli();
        List<Criteria> criteria = new ArrayList<>();
        if (!includeDrafts) {
            criteria.add(Criteria.where(STATUS_FIELD).nin(DRAFT, CANCELLED));
        }

        Criteria scopeCriteria = switch (scope == null ? "" : scope.toUpperCase()) {
            case "CURRENT" -> new Criteria().andOperator(
                    Criteria.where("startAt").lte(now),
                    Criteria.where("endAt").gte(now),
                    Criteria.where(STATUS_FIELD).nin(DRAFT, CANCELLED, FINISHED, EXHAUSTED)
            );
            case "UPCOMING" -> Criteria.where("startAt").gt(now);
            case "PAST" -> new Criteria().orOperator(
                    Criteria.where(STATUS_FIELD).in(FINISHED, EXHAUSTED, CANCELLED),
                    Criteria.where("endAt").lt(now)
            );
            default -> null;
        };
        if (scopeCriteria != null) {
            criteria.add(scopeCriteria);
        }

        return criteria.isEmpty()
                ? new Criteria()
                : new Criteria().andOperator(criteria.toArray(Criteria[]::new));
    }

    private EventStatusResponse toResponse(Event event) {
        String status = resolveDisplayStatus(event, Instant.now().toEpochMilli());
        Winner winner = firstWinner(event);
        EventStatusResponse.Builder builder = EventStatusResponse.newBuilder()
                .setEventId(nullToEmpty(event.getId()))
                .setKeysAvailable(event.getKeysAvailable())
                .setKeysTotal(event.getKeysTotal())
                .setStatus(status)
                .setEndDate(event.getEndAt())
                .setTitle(nullToEmpty(event.getTitle()))
                .setDescription(nullToEmpty(event.getDescription()))
                .setImageUrl(nullToEmpty(event.getImageUrl()))
                .setGameTitle(nullToEmpty(event.getGameTitle()))
                .setRawgGameId(event.getRawgGameId())
                .setPlatform(nullToEmpty(event.getPlatform()))
                .setStartAt(event.getStartAt())
                .setJoinDeadlineAt(event.getJoinDeadlineAt())
                .setRevealAt(event.getRevealAt())
                .setTotalSlots(event.getTotalSlots())
                .setAvailableSlots(event.getAvailableSlots())
                .setPublicChallengeCode("")
                .setCreatedByAdminId(nullToEmpty(event.getCreatedByAdminId()))
                .setWinnerUserId(winner == null ? nullToEmpty(event.getWinnerUserId()) : nullToEmpty(winner.getUserId()))
                .setWinnerUsername(winner == null ? nullToEmpty(event.getWinnerUsername()) : nullToEmpty(winner.getUsername()))
                .setFinishedAt(event.getFinishedAt() == null ? 0 : event.getFinishedAt())
                .setRewardSentAt(event.getRewardSentAt() == null ? 0 : event.getRewardSentAt())
                .setRewardDeliveryStatus(nullToEmpty(event.getRewardDeliveryStatus()))
                .setParticipantsCount(event.getParticipantsCount())
                .setRewardCodesTotal(event.getKeysTotal())
                .setRewardCodesAvailable(event.getKeysAvailable());
        if (event.getWinners() != null) {
            event.getWinners().forEach(item -> builder.addWinners(WinnerStatus.newBuilder()
                    .setUserId(nullToEmpty(item.getUserId()))
                    .setUsername(nullToEmpty(item.getUsername()))
                    .setClaimedAt(item.getClaimedAt() == null ? 0 : item.getClaimedAt())
                    .setDeliveryStatus(nullToEmpty(item.getDeliveryStatus()))
                    .build()));
        }
        return builder.build();
    }

    private String resolveDisplayStatus(Event event, long now) {
        if (DRAFT.equals(event.getStatus()) ||
                CANCELLED.equals(event.getStatus()) ||
                FINISHED.equals(event.getStatus()) ||
                EXHAUSTED.equals(event.getStatus())) {
            return event.getStatus();
        }

        if (now < event.getStartAt()) {
            return UPCOMING;
        }

        if (now <= event.getJoinDeadlineAt()) {
            return ACTIVE_JOIN;
        }

        if (now <= event.getEndAt()) {
            return now >= event.getRevealAt() ? REVEAL_ACTIVE : ACTIVE_JOIN;
        }

        return FINISHED;
    }

    private String initialPublishedStatus(long startAt, long now) {
        return now >= startAt ? ACTIVE_JOIN : UPCOMING;
    }

    private void validateEventPayload(
            String title,
            String gameTitle,
            String platform,
            long startAt,
            long joinDeadlineAt,
            long revealAt,
            long endAt,
            int totalSlots
    ) {
        if (title == null || title.isBlank()) {
            throw new IllegalArgumentException("Title is required.");
        }
        if (title.length() > MAX_SHORT_TEXT_LENGTH) {
            throw new IllegalArgumentException("Title is too long.");
        }
        if (gameTitle == null || gameTitle.isBlank()) {
            throw new IllegalArgumentException("Game title is required.");
        }
        if (gameTitle.length() > MAX_SHORT_TEXT_LENGTH) {
            throw new IllegalArgumentException("Game title is too long.");
        }
        if (platform == null || platform.isBlank()) {
            throw new IllegalArgumentException("Platform is required.");
        }
        if (platform.length() > MAX_SHORT_TEXT_LENGTH) {
            throw new IllegalArgumentException("Platform is too long.");
        }
        if (totalSlots <= 0) {
            throw new IllegalArgumentException("Total slots must be greater than zero.");
        }
        if (!(startAt < joinDeadlineAt && joinDeadlineAt <= revealAt && revealAt < endAt)) {
            throw new IllegalArgumentException("Event dates are invalid.");
        }
    }

    private void validateEventId(String eventId) {
        if (eventId == null || eventId.isBlank()) {
            throw new IllegalArgumentException("EventId is required.");
        }
    }

    private List<RewardCode> buildRewardCodes(List<String> accessKeys) {
        if (accessKeys == null || accessKeys.isEmpty()) {
            throw new IllegalArgumentException("At least one reward code is required.");
        }

        Set<String> uniqueCodes = new HashSet<>();
        List<RewardCode> rewardCodes = new ArrayList<>();
        for (String accessKey : accessKeys) {
            String code = accessKey == null ? "" : accessKey.trim();
            if (code.isBlank() || code.length() > MAX_REWARD_CODE_LENGTH) {
                throw new IllegalArgumentException("Reward codes are required and must be at most 50 characters.");
            }
            if (!uniqueCodes.add(code)) {
                throw new IllegalArgumentException("Reward codes must be unique.");
            }
            rewardCodes.add(RewardCode.builder()
                    .code(code)
                    .claimed(false)
                    .deliveryStatus(REWARD_PENDING)
                    .build());
        }
        return rewardCodes;
    }

    private boolean hasWinners(Event event) {
        return (event.getWinners() != null && !event.getWinners().isEmpty())
                || (event.getWinnerUserId() != null && !event.getWinnerUserId().isBlank());
    }

    private Optional<RewardCode> findAssignedCode(Event event, String userId) {
        if (event.getRewardCodes() == null) {
            return Optional.empty();
        }
        return event.getRewardCodes().stream()
                .filter(code -> userId.equals(code.getClaimedByUserId()))
                .findFirst();
    }

    private Optional<RewardCode> findAssignedCodeAt(Event event, long claimedAt) {
        if (event.getRewardCodes() == null) {
            return Optional.empty();
        }
        return event.getRewardCodes().stream()
                .filter(code -> code.getClaimedAt() != null && code.getClaimedAt() == claimedAt)
                .findFirst();
    }

    private Optional<RewardCode> findAnyClaimedCode(Event event) {
        if (event.getRewardCodes() == null) {
            return Optional.empty();
        }
        return event.getRewardCodes().stream()
                .filter(RewardCode::isClaimed)
                .findFirst();
    }

    private Winner firstWinner(Event event) {
        if (event.getWinners() != null && !event.getWinners().isEmpty()) {
            return event.getWinners().get(0);
        }
        if (event.getWinnerUserId() == null || event.getWinnerUserId().isBlank()) {
            return null;
        }
        return Winner.builder()
                .userId(event.getWinnerUserId())
                .username(event.getWinnerUsername())
                .claimedAt(event.getFinishedAt())
                .deliveryStatus(event.getRewardDeliveryStatus())
                .build();
    }

    private String nullToEmpty(String value) {
        return value == null ? "" : value;
    }

    private void rollbackParticipant(EventParticipant insertedParticipant) {
        if (insertedParticipant != null) {
            participantRepository.deleteByEventIdAndUserId(
                    insertedParticipant.getEventId(),
                    insertedParticipant.getUserId()
            );
        }
    }

    private void sendActionSuccess(StreamObserver<EventActionResponse> observer, String eventId, String message) {
        observer.onNext(EventActionResponse.newBuilder()
                .setSuccess(true)
                .setMessage(message)
                .setEventId(eventId)
                .build());
        observer.onCompleted();
    }

    private void sendActionError(StreamObserver<EventActionResponse> observer, String message) {
        observer.onNext(EventActionResponse.newBuilder()
                .setSuccess(false)
                .setMessage(message)
                .build());
        observer.onCompleted();
    }
}
