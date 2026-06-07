package com.spectrum.drops.service;

import com.spectrum.drops.grpc.*;
import com.spectrum.drops.model.Event;
import com.spectrum.drops.model.EventParticipant;
import com.spectrum.drops.model.RewardCode;
import com.spectrum.drops.model.Winner;
import com.spectrum.drops.repository.EventParticipantRepository;
import com.spectrum.drops.repository.EventRepository;
import io.grpc.stub.StreamObserver;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.data.mongodb.core.FindAndModifyOptions;
import org.springframework.data.mongodb.core.MongoTemplate;
import org.springframework.data.mongodb.core.query.Query;
import org.springframework.data.mongodb.core.query.UpdateDefinition;

import java.util.List;
import java.util.Optional;
import java.util.Queue;
import java.util.concurrent.ConcurrentLinkedQueue;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicReference;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.*;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
class DropsGrpcServiceTest {

    @Mock
    private EventRepository eventRepository;

    @Mock
    private EventParticipantRepository participantRepository;

    @Mock
    private MongoTemplate mongoTemplate;

    @InjectMocks
    private DropsGrpcService dropsGrpcService;

    @BeforeEach
    void setUp() {
        reset(eventRepository, participantRepository, mongoTemplate);
    }

    @Test
    void claimAccessKeyWhenRewardCodeIsAvailableShouldAssignWinner() {
        String eventId = "event-1";
        String userId = "user-1";
        when(participantRepository.existsByEventIdAndUserId(eventId, userId)).thenReturn(true);

        Event winner = activeEvent(eventId);
        winner.getRewardCodes().get(0).setClaimed(true);
        winner.getRewardCodes().get(0).setClaimedByUserId(userId);
        winner.getRewardCodes().get(0).setClaimedByUsername("spectrum");
        winner.getRewardCodes().get(0).setClaimedAt(getNow());
        winner.setKeysAvailable(1);
        winner.setStatus("REVEAL_ACTIVE");

        when(mongoTemplate.findAndModify(
                any(Query.class),
                any(UpdateDefinition.class),
                any(FindAndModifyOptions.class),
                eq(Event.class)))
                .thenReturn(winner);
        when(mongoTemplate.updateFirst(any(Query.class), any(UpdateDefinition.class), eq(Event.class)))
                .thenReturn(null);

        CapturingObserver<ClaimKeyResponse> observer = new CapturingObserver<>();
        dropsGrpcService.claimAccessKey(ClaimKeyRequest.newBuilder()
                .setEventId(eventId)
                .setUserId(userId)
                .setUsername("spectrum")
                .build(), observer);

        assertTrue(observer.value.getSuccess());
        assertEquals(userId, observer.value.getWinnerUserId());
        assertEquals("DEMO-KEY-1", observer.value.getAccessKeyCode());
        assertTrue(observer.completed);
    }

    @Test
    void claimAccessKeyWhenHundredUsersRaceShouldReturnOnlyOneWinner() throws InterruptedException {
        String eventId = "event-race";
        AtomicBoolean winnerAssigned = new AtomicBoolean(false);
        Queue<ClaimKeyResponse> responses = new ConcurrentLinkedQueue<>();

        when(participantRepository.existsByEventIdAndUserId(eq(eventId), anyString())).thenReturn(true);
        when(eventRepository.findById(eventId)).thenAnswer(invocation -> {
            Event event = activeEvent(eventId);
            event.setWinners(List.of(Winner.builder()
                    .userId("winner")
                    .username("winner-name")
                    .claimedAt(getNow())
                    .build()));
            event.setStatus("EXHAUSTED");
            return Optional.of(event);
        });
        when(mongoTemplate.findAndModify(
                any(Query.class),
                any(UpdateDefinition.class),
                any(FindAndModifyOptions.class),
                eq(Event.class)))
                .thenAnswer(invocation -> {
                    if (!winnerAssigned.compareAndSet(false, true)) {
                        return null;
                    }

                    Event event = activeEvent(eventId);
                    event.getRewardCodes().get(0).setClaimed(true);
                    event.getRewardCodes().get(0).setClaimedByUserId("winner");
                    event.getRewardCodes().get(0).setClaimedByUsername("winner-name");
                    event.getRewardCodes().get(0).setClaimedAt(getNow());
                    event.setKeysAvailable(0);
                    event.setStatus("EXHAUSTED");
                    return event;
                });
        when(mongoTemplate.updateFirst(any(Query.class), any(UpdateDefinition.class), eq(Event.class)))
                .thenReturn(null);

        int attempts = 100;
        CountDownLatch latch = new CountDownLatch(attempts);
        var executor = Executors.newFixedThreadPool(20);
        for (int index = 0; index < attempts; index++) {
            int userNumber = index;
            executor.submit(() -> {
                dropsGrpcService.claimAccessKey(ClaimKeyRequest.newBuilder()
                        .setEventId(eventId)
                        .setUserId("user-" + userNumber)
                        .setUsername("user-" + userNumber)
                        .build(), new StreamObserver<>() {
                    @Override
                    public void onNext(ClaimKeyResponse value) {
                        responses.add(value);
                    }

                    @Override
                    public void onError(Throwable throwable) {
                        latch.countDown();
                    }

                    @Override
                    public void onCompleted() {
                        latch.countDown();
                    }
                });
            });
        }

        assertTrue(latch.await(10, TimeUnit.SECONDS));
        executor.shutdownNow();

        assertEquals(attempts, responses.size());
        assertEquals(1, responses.stream().filter(ClaimKeyResponse::getSuccess).count());
    }

    @Test
    void claimAccessKeyWhenMultipleCodesExistShouldAllowMultipleDifferentWinners() {
        String eventId = "event-multi";
        when(participantRepository.existsByEventIdAndUserId(eq(eventId), anyString())).thenReturn(true);

        Event firstClaim = activeEvent(eventId);
        firstClaim.getRewardCodes().get(0).setClaimed(true);
        firstClaim.getRewardCodes().get(0).setClaimedByUserId("user-1");
        firstClaim.getRewardCodes().get(0).setClaimedByUsername("user-1");
        firstClaim.getRewardCodes().get(0).setClaimedAt(getNow());
        firstClaim.setKeysAvailable(1);

        Event secondClaim = activeEvent(eventId);
        secondClaim.getRewardCodes().get(1).setClaimed(true);
        secondClaim.getRewardCodes().get(1).setClaimedByUserId("user-2");
        secondClaim.getRewardCodes().get(1).setClaimedByUsername("user-2");
        secondClaim.getRewardCodes().get(1).setClaimedAt(getNow());
        secondClaim.setKeysAvailable(0);

        when(mongoTemplate.findAndModify(
                any(Query.class),
                any(UpdateDefinition.class),
                any(FindAndModifyOptions.class),
                eq(Event.class)))
                .thenReturn(firstClaim, secondClaim);
        when(mongoTemplate.updateFirst(any(Query.class), any(UpdateDefinition.class), eq(Event.class)))
                .thenReturn(null);

        CapturingObserver<ClaimKeyResponse> firstObserver = new CapturingObserver<>();
        dropsGrpcService.claimAccessKey(ClaimKeyRequest.newBuilder()
                .setEventId(eventId)
                .setUserId("user-1")
                .setUsername("user-1")
                .build(), firstObserver);

        CapturingObserver<ClaimKeyResponse> secondObserver = new CapturingObserver<>();
        dropsGrpcService.claimAccessKey(ClaimKeyRequest.newBuilder()
                .setEventId(eventId)
                .setUserId("user-2")
                .setUsername("user-2")
                .build(), secondObserver);

        assertTrue(firstObserver.value.getSuccess());
        assertTrue(secondObserver.value.getSuccess());
        assertNotEquals(firstObserver.value.getAccessKeyCode(), secondObserver.value.getAccessKeyCode());
    }

    @Test
    void claimAccessKeyWhenUserAlreadyClaimedShouldRejectWithoutSecondKey() {
        String eventId = "event-duplicate";
        String userId = "user-duplicate";
        when(participantRepository.existsByEventIdAndUserId(eventId, userId)).thenReturn(true);
        when(mongoTemplate.findAndModify(
                any(Query.class),
                any(UpdateDefinition.class),
                any(FindAndModifyOptions.class),
                eq(Event.class)))
                .thenReturn(null);

        Event event = activeEvent(eventId);
        event.setWinners(List.of(Winner.builder()
                .userId(userId)
                .username("already-winner")
                .claimedAt(getNow())
                .build()));
        when(eventRepository.findById(eventId)).thenReturn(Optional.of(event));

        CapturingObserver<ClaimKeyResponse> observer = new CapturingObserver<>();
        dropsGrpcService.claimAccessKey(ClaimKeyRequest.newBuilder()
                .setEventId(eventId)
                .setUserId(userId)
                .setUsername("already-winner")
                .build(), observer);

        assertFalse(observer.value.getSuccess());
        assertEquals("", observer.value.getAccessKeyCode());
        verify(mongoTemplate, never()).updateFirst(any(Query.class), any(UpdateDefinition.class), eq(Event.class));
    }

    @Test
    void claimAccessKeyWhenInventoryIsZeroShouldRejectAndNeverGoNegative() {
        String eventId = "event-empty";
        String userId = "user-empty";
        when(participantRepository.existsByEventIdAndUserId(eventId, userId)).thenReturn(true);
        when(mongoTemplate.findAndModify(
                any(Query.class),
                any(UpdateDefinition.class),
                any(FindAndModifyOptions.class),
                eq(Event.class)))
                .thenReturn(null);

        Event event = activeEvent(eventId);
        event.setKeysAvailable(0);
        event.setStatus("EXHAUSTED");
        when(eventRepository.findById(eventId)).thenReturn(Optional.of(event));

        CapturingObserver<ClaimKeyResponse> observer = new CapturingObserver<>();
        dropsGrpcService.claimAccessKey(ClaimKeyRequest.newBuilder()
                .setEventId(eventId)
                .setUserId(userId)
                .setUsername("empty")
                .build(), observer);

        assertFalse(observer.value.getSuccess());
        assertEquals(0, event.getKeysAvailable());
        verify(mongoTemplate, never()).updateFirst(any(Query.class), any(UpdateDefinition.class), eq(Event.class));
    }

    @Test
    void claimAccessKeyWhenHundredUsersRaceForTenKeysShouldReturnTenWinners() throws InterruptedException {
        String eventId = "event-ten-keys";
        AtomicInteger assigned = new AtomicInteger(0);
        Queue<ClaimKeyResponse> responses = new ConcurrentLinkedQueue<>();

        when(participantRepository.existsByEventIdAndUserId(eq(eventId), anyString())).thenReturn(true);
        when(eventRepository.findById(eventId)).thenReturn(Optional.of(exhaustedEvent(eventId)));
        when(mongoTemplate.findAndModify(
                any(Query.class),
                any(UpdateDefinition.class),
                any(FindAndModifyOptions.class),
                eq(Event.class)))
                .thenAnswer(invocation -> {
                    int number = assigned.incrementAndGet();
                    if (number > 10) {
                        return null;
                    }

                    Event event = eventWithTenKeys(eventId);
                    RewardCode code = event.getRewardCodes().get(number - 1);
                    code.setClaimed(true);
                    code.setClaimedByUserId("winner-" + number);
                    code.setClaimedAt(getNow());
                    event.setKeysAvailable(10 - number);
                    event.setStatus(number == 10 ? "EXHAUSTED" : "REVEAL_ACTIVE");
                    return event;
                });
        when(mongoTemplate.updateFirst(any(Query.class), any(UpdateDefinition.class), eq(Event.class)))
                .thenReturn(null);

        int attempts = 100;
        CountDownLatch latch = new CountDownLatch(attempts);
        var executor = Executors.newFixedThreadPool(25);
        for (int index = 0; index < attempts; index++) {
            int userNumber = index;
            executor.submit(() -> {
                dropsGrpcService.claimAccessKey(ClaimKeyRequest.newBuilder()
                        .setEventId(eventId)
                        .setUserId("user-" + userNumber)
                        .setUsername("user-" + userNumber)
                        .build(), new StreamObserver<>() {
                    @Override
                    public void onNext(ClaimKeyResponse value) {
                        responses.add(value);
                    }

                    @Override
                    public void onError(Throwable throwable) {
                        latch.countDown();
                    }

                    @Override
                    public void onCompleted() {
                        latch.countDown();
                    }
                });
            });
        }

        assertTrue(latch.await(10, TimeUnit.SECONDS));
        executor.shutdownNow();

        assertEquals(attempts, responses.size());
        assertEquals(10, responses.stream().filter(ClaimKeyResponse::getSuccess).count());
        assertEquals(90, responses.stream().filter(response -> !response.getSuccess()).count());
    }

    @Test
    void getWonKeysShouldReturnOnlyKeysForRequestedUser() {
        String userId = "user-owner";
        Event first = exhaustedEvent("event-owned");
        first.setGameTitle("Halo");
        first.setWinners(List.of(
                Winner.builder().userId(userId).username("owner").rewardCode("OWN-KEY").claimedAt(1000L).deliveryStatus("PENDING").build(),
                Winner.builder().userId("other").username("other").rewardCode("OTHER-KEY").claimedAt(1001L).deliveryStatus("PENDING").build()
        ));
        Event second = exhaustedEvent("event-other");
        second.setWinners(List.of(
                Winner.builder().userId("other").username("other").rewardCode("OTHER-ONLY").claimedAt(1002L).deliveryStatus("PENDING").build()
        ));
        when(mongoTemplate.find(any(Query.class), eq(Event.class))).thenReturn(List.of(first, second));

        CapturingObserver<WonKeysResponse> observer = new CapturingObserver<>();
        dropsGrpcService.getWonKeys(WonKeysRequest.newBuilder().setUserId(userId).build(), observer);

        assertEquals(1, observer.value.getWonKeysCount());
        assertEquals("event-owned", observer.value.getWonKeys(0).getEventId());
        assertEquals("OWN-KEY", observer.value.getWonKeys(0).getAccessKeyCode());
    }

    @Test
    void joinEventWhenSlotAvailableShouldCreateParticipationAndDecrementInventory() {
        String eventId = "event-join";
        String userId = "user-1";
        when(participantRepository.existsByEventIdAndUserId(eventId, userId)).thenReturn(false);
        when(participantRepository.save(any(EventParticipant.class))).thenAnswer(invocation -> invocation.getArgument(0));

        Event updated = activeEvent(eventId);
        updated.setAvailableSlots(9);
        updated.setParticipantsCount(1);

        when(mongoTemplate.findAndModify(
                any(Query.class),
                any(UpdateDefinition.class),
                any(FindAndModifyOptions.class),
                eq(Event.class)))
                .thenReturn(updated);

        CapturingObserver<EventActionResponse> observer = new CapturingObserver<>();
        dropsGrpcService.joinEvent(JoinEventRequest.newBuilder()
                .setEventId(eventId)
                .setUserId(userId)
                .build(), observer);

        assertTrue(observer.value.getSuccess());
        verify(participantRepository).save(any(EventParticipant.class));
    }

    @Test
    void joinEventWhenUserAlreadyJoinedShouldRejectWithoutAtomicSlotUpdate() {
        String eventId = "event-duplicate-join";
        String userId = "user-1";
        when(participantRepository.existsByEventIdAndUserId(eventId, userId)).thenReturn(true);

        CapturingObserver<EventActionResponse> observer = new CapturingObserver<>();
        dropsGrpcService.joinEvent(JoinEventRequest.newBuilder()
                .setEventId(eventId)
                .setUserId(userId)
                .build(), observer);

        assertFalse(observer.value.getSuccess());
        assertEquals("duplicateParticipation", observer.value.getMessage());
        verify(participantRepository, never()).save(any(EventParticipant.class));
        verify(mongoTemplate, never()).findAndModify(any(Query.class), any(UpdateDefinition.class), any(FindAndModifyOptions.class), eq(Event.class));
    }

    @Test
    void joinEventWhenAtomicSlotUpdateFailsShouldRollbackInsertedParticipant() {
        String eventId = "event-full";
        String userId = "user-1";
        when(participantRepository.existsByEventIdAndUserId(eventId, userId)).thenReturn(false);
        when(mongoTemplate.findAndModify(
                any(Query.class),
                any(UpdateDefinition.class),
                any(FindAndModifyOptions.class),
                eq(Event.class)))
                .thenReturn(null);

        CapturingObserver<EventActionResponse> observer = new CapturingObserver<>();
        dropsGrpcService.joinEvent(JoinEventRequest.newBuilder()
                .setEventId(eventId)
                .setUserId(userId)
                .build(), observer);

        assertFalse(observer.value.getSuccess());
        assertEquals("Event is not accepting participants.", observer.value.getMessage());
        verify(participantRepository).save(any(EventParticipant.class));
        verify(participantRepository, atLeastOnce()).deleteByEventIdAndUserId(eventId, userId);
    }

    @Test
    void createEventWhenDatesAreInvalidShouldReturnError() {
        long now = getNow();
        CapturingObserver<EventActionResponse> observer = new CapturingObserver<>();

        dropsGrpcService.createEvent(CreateEventRequest.newBuilder()
                .setTitle("Invalid")
                .setGameTitle("Halo")
                .setPlatform("PC")
                .setStartAt(now + 2000)
                .setJoinDeadlineAt(now + 1000)
                .setRevealAt(now + 3000)
                .setEndAt(now + 4000)
                .setTotalSlots(10)
                .addAccessKeys("DEMO-KEY-1")
                .build(), observer);

        assertFalse(observer.value.getSuccess());
        verify(eventRepository, never()).save(any());
    }

    @Test
    void createEventShouldAlwaysPublishAutomatically() {
        long now = getNow();
        AtomicReference<Event> saved = new AtomicReference<>();
        when(eventRepository.save(any(Event.class))).thenAnswer(invocation -> {
            Event event = invocation.getArgument(0);
            event.setId("event-created");
            saved.set(event);
            return event;
        });

        CapturingObserver<EventActionResponse> observer = new CapturingObserver<>();
        dropsGrpcService.createEvent(CreateEventRequest.newBuilder()
                .setTitle("Launch")
                .setGameTitle("Halo")
                .setPlatform("PC")
                .setStartAt(now + 20_000)
                .setJoinDeadlineAt(now + 40_000)
                .setRevealAt(now + 50_000)
                .setEndAt(now + 60_000)
                .setTotalSlots(10)
                .setPublishNow(false)
                .addAccessKeys("DHA3-SDFE-32EF-SF5R")
                .build(), observer);

        assertTrue(observer.value.getSuccess());
        assertEquals("UPCOMING", saved.get().getStatus());
    }

    @Test
    void createEventWhenRewardCodeFormatIsInvalidShouldReturnError() {
        long now = getNow();
        CapturingObserver<EventActionResponse> observer = new CapturingObserver<>();

        dropsGrpcService.createEvent(CreateEventRequest.newBuilder()
                .setTitle("Invalid code")
                .setGameTitle("Halo")
                .setPlatform("PC")
                .setStartAt(now + 20_000)
                .setJoinDeadlineAt(now + 40_000)
                .setRevealAt(now + 50_000)
                .setEndAt(now + 60_000)
                .setTotalSlots(10)
                .addAccessKeys("DEMO-KEY-1")
                .build(), observer);

        assertFalse(observer.value.getSuccess());
        assertTrue(observer.value.getMessage().contains("XXXX-XXXX-XXXX-XXXX"));
        verify(eventRepository, never()).save(any());
    }

    @Test
    void createEventWhenRewardCodesAreDuplicatedShouldReturnError() {
        long now = getNow();
        CapturingObserver<EventActionResponse> observer = new CapturingObserver<>();

        dropsGrpcService.createEvent(CreateEventRequest.newBuilder()
                .setTitle("Duplicated code")
                .setGameTitle("Halo")
                .setPlatform("PC")
                .setStartAt(now + 20_000)
                .setJoinDeadlineAt(now + 40_000)
                .setRevealAt(now + 50_000)
                .setEndAt(now + 60_000)
                .setTotalSlots(10)
                .addAccessKeys("DHA3-SDFE-32EF-SF5R")
                .addAccessKeys("dha3-sdfe-32ef-sf5r")
                .build(), observer);

        assertFalse(observer.value.getSuccess());
        assertEquals("Reward codes must be unique.", observer.value.getMessage());
        verify(eventRepository, never()).save(any());
    }

    @Test
    void updateEventWhenEditableShouldUpdateFieldsAndRecalculateRewardInventory() {
        String eventId = "event-update";
        long now = getNow();
        Event event = activeEvent(eventId);
        event.setStatus("UPCOMING");
        event.setStartAt(now + 30 * 60_000);
        event.setParticipantsCount(2);
        event.getRewardCodes().get(0).setClaimed(true);
        event.setKeysAvailable(1);
        when(eventRepository.findById(eventId)).thenReturn(Optional.of(event));
        when(eventRepository.save(any(Event.class))).thenAnswer(invocation -> invocation.getArgument(0));

        CapturingObserver<EventActionResponse> observer = new CapturingObserver<>();
        dropsGrpcService.updateEvent(UpdateEventRequest.newBuilder()
                .setEventId(eventId)
                .setTitle("  Updated title  ")
                .setDescription("Updated description")
                .setImageUrl("https://example.test/updated.jpg")
                .setGameTitle("Halo Infinite")
                .setRawgGameId(123)
                .setPlatform("PC")
                .setStartAt(now + 30 * 60_000)
                .setJoinDeadlineAt(now + 40 * 60_000)
                .setRevealAt(now + 50 * 60_000)
                .setEndAt(now + 60 * 60_000)
                .setTotalSlots(5)
                .build(), observer);

        assertTrue(observer.value.getSuccess());
        assertEquals("Updated title", event.getTitle());
        assertEquals(3, event.getAvailableSlots());
        assertEquals(2, event.getKeysTotal());
        assertEquals(1, event.getKeysAvailable());
        assertEquals("", event.getPublicChallengeCode());
        verify(eventRepository).save(event);
    }

    @Test
    void updateEventWhenTotalSlotsWouldBeBelowParticipantsShouldReturnError() {
        String eventId = "event-update-slots";
        long now = getNow();
        Event event = activeEvent(eventId);
        event.setStatus("UPCOMING");
        event.setStartAt(now + 30 * 60_000);
        event.setParticipantsCount(4);
        when(eventRepository.findById(eventId)).thenReturn(Optional.of(event));

        CapturingObserver<EventActionResponse> observer = new CapturingObserver<>();
        dropsGrpcService.updateEvent(UpdateEventRequest.newBuilder()
                .setEventId(eventId)
                .setTitle("Updated")
                .setGameTitle("Halo")
                .setPlatform("PC")
                .setStartAt(now + 30 * 60_000)
                .setJoinDeadlineAt(now + 40 * 60_000)
                .setRevealAt(now + 50 * 60_000)
                .setEndAt(now + 60 * 60_000)
                .setTotalSlots(3)
                .build(), observer);

        assertFalse(observer.value.getSuccess());
        assertEquals("Total slots cannot be lower than current participants.", observer.value.getMessage());
        verify(eventRepository, never()).save(any(Event.class));
    }

    @Test
    void publishEventWhenEventIsEditableShouldSetPublishedStatusAndSave() {
        String eventId = "event-publish";
        Event event = activeEvent(eventId);
        event.setStartAt(getNow() + 60_000);
        event.setStatus("DRAFT");
        when(eventRepository.findById(eventId)).thenReturn(Optional.of(event));

        CapturingObserver<EventActionResponse> observer = new CapturingObserver<>();
        dropsGrpcService.publishEvent(PublishEventRequest.newBuilder().setEventId(eventId).build(), observer);

        assertTrue(observer.value.getSuccess());
        assertEquals("UPCOMING", event.getStatus());
        verify(eventRepository).save(event);
    }

    @Test
    void publishEventWhenEventAlreadyFinishedShouldReturnErrorWithoutSave() {
        String eventId = "event-finished";
        Event event = exhaustedEvent(eventId);
        event.setStatus("FINISHED");
        when(eventRepository.findById(eventId)).thenReturn(Optional.of(event));

        CapturingObserver<EventActionResponse> observer = new CapturingObserver<>();
        dropsGrpcService.publishEvent(PublishEventRequest.newBuilder().setEventId(eventId).build(), observer);

        assertFalse(observer.value.getSuccess());
        assertEquals("Finished or cancelled events cannot be published.", observer.value.getMessage());
        verify(eventRepository, never()).save(any(Event.class));
    }

    @Test
    void finishEventWithoutWinnerAndCancelFlagShouldCancelEvent() {
        String eventId = "event-cancel";
        Event event = activeEvent(eventId);
        when(eventRepository.findById(eventId)).thenReturn(Optional.of(event));

        CapturingObserver<EventActionResponse> observer = new CapturingObserver<>();
        dropsGrpcService.finishEvent(FinishEventRequest.newBuilder()
                .setEventId(eventId)
                .setCancelIfWithoutWinner(true)
                .build(), observer);

        assertTrue(observer.value.getSuccess());
        assertEquals("CANCELLED", event.getStatus());
        assertNotNull(event.getFinishedAt());
        verify(eventRepository).save(event);
    }

    @Test
    void finishEventWhenAlreadyClosedShouldReturnSuccessWithoutSavingAgain() {
        String eventId = "event-closed";
        Event event = exhaustedEvent(eventId);
        when(eventRepository.findById(eventId)).thenReturn(Optional.of(event));

        CapturingObserver<EventActionResponse> observer = new CapturingObserver<>();
        dropsGrpcService.finishEvent(FinishEventRequest.newBuilder().setEventId(eventId).build(), observer);

        assertTrue(observer.value.getSuccess());
        assertEquals("Event already closed.", observer.value.getMessage());
        verify(eventRepository, never()).save(any(Event.class));
    }

    @Test
    void getEventStatusWhenEventExistsShouldReturnDetails() {
        Event event = activeEvent("event-1");
        when(eventRepository.findById("event-1")).thenReturn(Optional.of(event));

        CapturingObserver<EventStatusResponse> observer = new CapturingObserver<>();
        dropsGrpcService.getEventStatus(GetEventRequest.newBuilder().setEventId("event-1").build(), observer);

        assertEquals("event-1", observer.value.getEventId());
        assertEquals("REGISTRATION_OPEN", observer.value.getStatus());
        assertEquals(10, observer.value.getTotalSlots());
    }

    @Test
    void getEventStatusWhenEventHasNotStartedShouldReturnUpcomingAndNoJoinAction() {
        String eventId = "event-upcoming";
        long now = getNow();
        Event event = activeEvent(eventId);
        event.setStartAt(now + 60_000);
        event.setJoinDeadlineAt(now + 120_000);
        event.setRevealAt(now + 180_000);
        event.setEndAt(now + 240_000);
        event.setStatus("UPCOMING");
        when(eventRepository.findById(eventId)).thenReturn(Optional.of(event));

        CapturingObserver<EventStatusResponse> observer = new CapturingObserver<>();
        dropsGrpcService.getEventStatus(GetEventRequest.newBuilder()
                .setEventId(eventId)
                .setRequesterUserId("user-1")
                .build(), observer);

        assertEquals("UPCOMING", observer.value.getStatus());
        assertFalse(observer.value.getCanJoin());
        assertFalse(observer.value.getCanClaim());
    }

    @Test
    void getEventStatusWhenRequesterJoinedAfterRevealShouldAllowClaim() {
        String eventId = "event-claim-ready";
        String userId = "user-ready";
        Event event = activeEvent(eventId);
        long now = getNow();
        event.setJoinDeadlineAt(now - 5_000);
        event.setRevealAt(now - 1_000);
        event.setEndAt(now + 20_000);
        when(eventRepository.findById(eventId)).thenReturn(Optional.of(event));
        when(participantRepository.existsByEventIdAndUserId(eventId, userId)).thenReturn(true);

        CapturingObserver<EventStatusResponse> observer = new CapturingObserver<>();
        dropsGrpcService.getEventStatus(GetEventRequest.newBuilder()
                .setEventId(eventId)
                .setRequesterUserId(userId)
                .build(), observer);

        assertEquals("REVEAL_READY", observer.value.getStatus());
        assertTrue(observer.value.getCurrentUserJoined());
        assertTrue(observer.value.getCanClaim());
    }

    @Test
    void getEventStatusShouldNeverExposeChallengeOrRewardCodesInPublicStatus() {
        String eventId = "event-safe-status";
        Event event = activeEvent(eventId);
        event.setPublicChallengeCode("SECRET-CHALLENGE");
        event.setWinners(List.of(Winner.builder()
                .userId("winner-1")
                .username("winner")
                .rewardCode("DHA3-SDFE-32EF-SF5R")
                .claimedAt(getNow())
                .deliveryStatus("SENT")
                .build()));
        when(eventRepository.findById(eventId)).thenReturn(Optional.of(event));

        CapturingObserver<EventStatusResponse> observer = new CapturingObserver<>();
        dropsGrpcService.getEventStatus(GetEventRequest.newBuilder().setEventId(eventId).build(), observer);

        assertEquals("", observer.value.getPublicChallengeCode());
        assertEquals(1, observer.value.getWinnersCount());
        assertEquals("winner", observer.value.getWinners(0).getUsername());
        assertEquals("winner-1", observer.value.getWinners(0).getUserId());
    }

    @Test
    void listEventsShouldReturnPagedEventsWithRequesterJoinedFlags() {
        String requesterId = "user-1";
        Event first = activeEvent("event-joined");
        Event second = activeEvent("event-open");
        List<Event> events = List.of(first, second);
        when(mongoTemplate.count(any(Query.class), eq(Event.class))).thenReturn(2L);
        when(mongoTemplate.find(any(Query.class), eq(Event.class))).thenReturn(events);
        when(participantRepository.findByUserId(requesterId)).thenReturn(List.of(
                EventParticipant.builder()
                        .eventId("event-joined")
                        .userId(requesterId)
                        .joinedAt(getNow())
                        .build()
        ));

        CapturingObserver<EventListResponse> observer = new CapturingObserver<>();
        dropsGrpcService.listEvents(ListEventsRequest.newBuilder()
                .setScope("CURRENT")
                .setPage(0)
                .setPageSize(100)
                .setRequesterUserId(requesterId)
                .build(), observer);

        assertEquals(2, observer.value.getTotalCount());
        assertEquals(1, observer.value.getPage());
        assertEquals(50, observer.value.getPageSize());
        assertTrue(observer.value.getEvents(0).getCurrentUserJoined());
        assertFalse(observer.value.getEvents(1).getCurrentUserJoined());
    }

    @Test
    void claimAccessKeyWhenUserIsNotRegisteredShouldRejectWithoutAtomicClaim() {
        String eventId = "event-unregistered";
        String userId = "user-not-joined";
        when(participantRepository.existsByEventIdAndUserId(eventId, userId)).thenReturn(false);

        CapturingObserver<ClaimKeyResponse> observer = new CapturingObserver<>();
        dropsGrpcService.claimAccessKey(ClaimKeyRequest.newBuilder()
                .setEventId(eventId)
                .setUserId(userId)
                .setUsername("not-joined")
                .build(), observer);

        assertFalse(observer.value.getSuccess());
        assertEquals("", observer.value.getAccessKeyCode());
        assertEquals("User must join before claiming.", observer.value.getMessage());
        verify(mongoTemplate, never()).findAndModify(any(Query.class), any(UpdateDefinition.class), any(FindAndModifyOptions.class), eq(Event.class));
    }

    @Test
    void getEventStatusWhenStoredFinishedDuringRevealWithCodesShouldStillAllowClaim() {
        String eventId = "event-stale-finished";
        String userId = "user-ready";
        Event event = activeEvent(eventId);
        long now = getNow();
        event.setStatus("FINISHED");
        event.setJoinDeadlineAt(now - 5_000);
        event.setRevealAt(now - 1_000);
        event.setEndAt(now + 20_000);
        event.setKeysAvailable(1);
        when(eventRepository.findById(eventId)).thenReturn(Optional.of(event));
        when(participantRepository.existsByEventIdAndUserId(eventId, userId)).thenReturn(true);

        CapturingObserver<EventStatusResponse> observer = new CapturingObserver<>();
        dropsGrpcService.getEventStatus(GetEventRequest.newBuilder()
                .setEventId(eventId)
                .setRequesterUserId(userId)
                .build(), observer);

        assertEquals("REVEAL_READY", observer.value.getStatus());
        assertTrue(observer.value.getCanClaim());
    }

    @Test
    void markRewardSentWhenWinnerUserMatchesShouldUpdateDeliveryStatus() {
        String eventId = "event-sent";
        Event updated = activeEvent(eventId);
        updated.setWinners(List.of(Winner.builder()
                .userId("winner-1")
                .username("winner")
                .rewardCode("KEY-1")
                .claimedAt(getNow())
                .deliveryStatus("SENT")
                .build()));
        when(mongoTemplate.findAndModify(
                any(Query.class),
                any(UpdateDefinition.class),
                any(FindAndModifyOptions.class),
                eq(Event.class)))
                .thenReturn(updated);

        CapturingObserver<EventActionResponse> observer = new CapturingObserver<>();
        dropsGrpcService.markRewardSent(MarkRewardSentRequest.newBuilder()
                .setEventId(eventId)
                .setWinnerUserId("winner-1")
                .setRewardSentAt(getNow())
                .build(), observer);

        assertTrue(observer.value.getSuccess());
        verify(mongoTemplate).findAndModify(any(Query.class), any(UpdateDefinition.class), any(FindAndModifyOptions.class), eq(Event.class));
    }

    @Test
    void markRewardDeliveryFailedWhenWinnerUserMatchesShouldUpdateDeliveryStatus() {
        String eventId = "event-failed";
        Event updated = activeEvent(eventId);
        updated.setWinners(List.of(Winner.builder()
                .userId("winner-1")
                .username("winner")
                .rewardCode("KEY-1")
                .claimedAt(getNow())
                .deliveryStatus("FAILED")
                .build()));
        when(mongoTemplate.findAndModify(
                any(Query.class),
                any(UpdateDefinition.class),
                any(FindAndModifyOptions.class),
                eq(Event.class)))
                .thenReturn(updated);

        CapturingObserver<EventActionResponse> observer = new CapturingObserver<>();
        dropsGrpcService.markRewardDeliveryFailed(MarkRewardDeliveryFailedRequest.newBuilder()
                .setEventId(eventId)
                .setWinnerUserId("winner-1")
                .setFailedAt(getNow())
                .build(), observer);

        assertTrue(observer.value.getSuccess());
        verify(mongoTemplate).findAndModify(any(Query.class), any(UpdateDefinition.class), any(FindAndModifyOptions.class), eq(Event.class));
    }

    private static Event activeEvent(String eventId) {
        long now = getNow();
        Event event = new Event();
        event.setId(eventId);
        event.setTitle("Launch Drop");
        event.setDescription("Reward");
        event.setGameTitle("Halo");
        event.setPlatform("PC");
        event.setImageUrl("https://example.test/halo.jpg");
        event.setStatus("ACTIVE");
        event.setStartAt(now - 1_000);
        event.setJoinDeadlineAt(now + 10_000);
        event.setRevealAt(now - 500);
        event.setEndAt(now + 20_000);
        event.setTotalSlots(10);
        event.setAvailableSlots(10);
        event.setKeysTotal(2);
        event.setKeysAvailable(2);
        event.setRewardCodes(List.of(
                RewardCode.builder().code("DEMO-KEY-1").claimed(false).deliveryStatus("PENDING").build(),
                RewardCode.builder().code("DEMO-KEY-2").claimed(false).deliveryStatus("PENDING").build()
        ));
        event.setWinners(List.of());
        event.setPublicChallengeCode("");
        event.setRewardDeliveryStatus("PENDING");
        return event;
    }

    private static Event eventWithTenKeys(String eventId) {
        Event event = activeEvent(eventId);
        event.setKeysTotal(10);
        event.setKeysAvailable(10);
        event.setRewardCodes(java.util.stream.IntStream.rangeClosed(1, 10)
                .mapToObj(number -> RewardCode.builder()
                        .code("DEMO-KEY-" + number)
                        .claimed(false)
                        .deliveryStatus("PENDING")
                        .build())
                .toList());
        return event;
    }

    private static Event exhaustedEvent(String eventId) {
        Event event = activeEvent(eventId);
        event.setKeysAvailable(0);
        event.setStatus("EXHAUSTED");
        return event;
    }

    private static class CapturingObserver<T> implements StreamObserver<T> {
        private T value;
        private boolean completed;

        @Override
        public void onNext(T value) {
            this.value = value;
        }

        @Override
        public void onError(Throwable throwable) {
            fail(throwable);
        }

        @Override
        public void onCompleted() {
            completed = true;
        }
    }

    @SuppressWarnings("java:S3688")
    private static long getNow() {
        return System.currentTimeMillis();
    }
}
