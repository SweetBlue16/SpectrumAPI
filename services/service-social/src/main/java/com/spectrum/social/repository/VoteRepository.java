package com.spectrum.social.repository;

import com.spectrum.social.model.Vote;
import org.springframework.data.mongodb.repository.MongoRepository;

import java.util.Optional;

public interface VoteRepository extends MongoRepository<Vote, String> {
    Optional<Vote> findByUserIdAndTargetIdAndTargetType(String userId, String targetId, String targetType);
}
