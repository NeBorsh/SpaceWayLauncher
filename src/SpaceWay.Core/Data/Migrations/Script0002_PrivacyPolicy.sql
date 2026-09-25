CREATE TABLE AcceptedPrivacyPolicy(
    Identifier    TEXT NOT NULL PRIMARY KEY,

    Version       TEXT NOT NULL,

    AcceptedAt    TEXT NOT NULL,

    LastConnected TEXT NOT NULL
);
