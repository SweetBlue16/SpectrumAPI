package com.spectrum.social.repository;

import com.spectrum.social.model.Report;
import org.springframework.data.mongodb.repository.MongoRepository;

import java.util.List;

public interface ReportRepository extends MongoRepository<Report, String> {
    List<Report> findByStatus(String status);
}
