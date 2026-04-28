package com.spectrum.drops.repository;


import com.spectrum.drops.model.Event;
import org.springframework.data.mongodb.repository.MongoRepository;

public interface EventRepository extends MongoRepository<Event, String> {
}
