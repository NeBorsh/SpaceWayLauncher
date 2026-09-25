CREATE TABLE InstalledEngine(
    Version   TEXT NOT NULL PRIMARY KEY,

    Signature TEXT NOT NULL
);

CREATE TABLE InstalledEngineModule(
    Name    TEXT NOT NULL,
    Version TEXT NOT NULL,

    PRIMARY KEY (Name, Version)
);
