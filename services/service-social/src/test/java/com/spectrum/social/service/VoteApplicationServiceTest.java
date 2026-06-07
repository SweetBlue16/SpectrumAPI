package com.spectrum.social.service;

import com.spectrum.social.model.Vote;
import com.spectrum.social.repository.VoteRepository;
import org.junit.jupiter.api.Test;

import java.util.List;
import java.util.Optional;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.*;

class VoteApplicationServiceTest {

    @Test
    void castVoteWhenUserHasNoVoteShouldCreateVoteAndReturnCounts() {
        VoteRepository repository = mock(VoteRepository.class);
        when(repository.findByUserIdAndTargetIdAndTargetType("user-1", "review-1", "REVIEW"))
                .thenReturn(Optional.empty());
        when(repository.countByTargetIdAndTargetTypeAndPositive("review-1", "REVIEW", true)).thenReturn(1L);
        when(repository.countByTargetIdAndTargetTypeAndPositive("review-1", "REVIEW", false)).thenReturn(0L);
        VoteApplicationService service = new VoteApplicationService(repository);

        VoteApplicationService.VoteCounts counts = service.castVote("user-1", "review-1", "REVIEW", true);

        assertEquals(1, counts.updatedLikes());
        assertEquals(0, counts.updatedDislikes());
        verify(repository).save(argThat(vote ->
                "user-1".equals(vote.getUserId()) &&
                        "review-1".equals(vote.getTargetId()) &&
                        "REVIEW".equals(vote.getTargetType()) &&
                        vote.isPositive()));
        verify(repository, never()).delete(any());
    }

    @Test
    void castVoteWhenUserRepeatsSameVoteShouldRemoveVote() {
        Vote existing = Vote.builder()
                .userId("user-1")
                .targetId("review-1")
                .targetType("REVIEW")
                .positive(true)
                .build();
        VoteRepository repository = mock(VoteRepository.class);
        when(repository.findByUserIdAndTargetIdAndTargetType("user-1", "review-1", "REVIEW"))
                .thenReturn(Optional.of(existing));
        when(repository.countByTargetIdAndTargetTypeAndPositive("review-1", "REVIEW", true)).thenReturn(0L);
        when(repository.countByTargetIdAndTargetTypeAndPositive("review-1", "REVIEW", false)).thenReturn(0L);
        VoteApplicationService service = new VoteApplicationService(repository);

        VoteApplicationService.VoteCounts counts = service.castVote("user-1", "review-1", "REVIEW", true);

        assertEquals(0, counts.updatedLikes());
        assertEquals(0, counts.updatedDislikes());
        verify(repository).delete(existing);
        verify(repository, never()).save(any());
    }

    @Test
    void castVoteWhenUserChangesVoteShouldUpdateExistingVote() {
        Vote existing = Vote.builder()
                .userId("user-1")
                .targetId("review-1")
                .targetType("REVIEW")
                .positive(true)
                .build();
        VoteRepository repository = mock(VoteRepository.class);
        when(repository.findByUserIdAndTargetIdAndTargetType("user-1", "review-1", "REVIEW"))
                .thenReturn(Optional.of(existing));
        when(repository.countByTargetIdAndTargetTypeAndPositive("review-1", "REVIEW", true)).thenReturn(0L);
        when(repository.countByTargetIdAndTargetTypeAndPositive("review-1", "REVIEW", false)).thenReturn(1L);
        VoteApplicationService service = new VoteApplicationService(repository);

        VoteApplicationService.VoteCounts counts = service.castVote("user-1", "review-1", "REVIEW", false);

        assertEquals(0, counts.updatedLikes());
        assertEquals(1, counts.updatedDislikes());
        assertEquals(false, existing.isPositive());
        verify(repository).save(existing);
        verify(repository, never()).delete(any());
    }

    @Test
    void getUserVotesWhenTargetsAreEmptyShouldReturnEmptyListWithoutRepositoryCall() {
        VoteRepository repository = mock(VoteRepository.class);
        VoteApplicationService service = new VoteApplicationService(repository);

        List<Vote> result = service.getUserVotes("user-1", "REVIEW", List.of());

        assertTrue(result.isEmpty());
        verify(repository, never()).findByUserIdAndTargetTypeAndTargetIdIn(any(), any(), any());
    }
}
