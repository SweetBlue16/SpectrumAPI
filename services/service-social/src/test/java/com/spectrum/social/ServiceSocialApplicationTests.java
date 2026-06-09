package com.spectrum.social;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertDoesNotThrow;

class ServiceSocialApplicationTests {

    @Test
    void applicationClassCanBeLoaded() {
        assertDoesNotThrow(() -> Class.forName(ServiceSocialApplication.class.getName()));
    }

}
