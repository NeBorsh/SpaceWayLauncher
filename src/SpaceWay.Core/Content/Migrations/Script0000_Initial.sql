CREATE TABLE ContentVersion(
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,

    Hash        BLOB NOT NULL,

    ForkId      TEXT NULL,
    ForkVersion TEXT NULL,

    LastUsed    TEXT NOT NULL,

    ZipHash     BLOB NULL
);

CREATE TABLE Content(
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,

    Hash        BLOB NOT NULL UNIQUE,

    Size        INTEGER NOT NULL,

    Compression INTEGER NOT NULL,

    Data        BLOB NOT NULL,

    CONSTRAINT UncompressedSameSize CHECK(Compression != 0 OR length(Data) = Size)
);

CREATE TABLE ContentManifest(
    Id        INTEGER PRIMARY KEY,
    VersionId INTEGER NOT NULL REFERENCES ContentVersion(Id) ON DELETE CASCADE,
    Path      TEXT NOT NULL,

    ContentId INTEGER NOT NULL REFERENCES Content(Id) ON DELETE RESTRICT,

    CONSTRAINT NotDirectory CHECK (Path NOT LIKE '%/')
);

CREATE UNIQUE INDEX IX_ContentManifest_Unique ON ContentManifest(VersionId, Path);

CREATE INDEX IX_ContentManifest_Content ON ContentManifest(ContentId);

CREATE TABLE ContentEngineDependency(
    Id            INTEGER PRIMARY KEY,
    VersionId     INTEGER NOT NULL REFERENCES ContentVersion(Id) ON DELETE CASCADE,
    ModuleName    TEXT NOT NULL,
    ModuleVersion TEXT NOT NULL
);

CREATE UNIQUE INDEX IX_ContentEngineDependency_Unique
    ON ContentEngineDependency(VersionId, ModuleName);

CREATE TABLE InterruptedDownload(
    Id    INTEGER PRIMARY KEY AUTOINCREMENT,
    Added TEXT NOT NULL
);

CREATE TABLE InterruptedDownloadContent(
    Id                    INTEGER PRIMARY KEY AUTOINCREMENT,
    InterruptedDownloadId INTEGER NOT NULL REFERENCES InterruptedDownload(Id) ON DELETE CASCADE,

    ContentId             INTEGER NOT NULL UNIQUE REFERENCES Content(Id) ON DELETE CASCADE
);

CREATE TABLE RunningClient(
    ProcessId   INTEGER PRIMARY KEY NOT NULL,

    MainModule  TEXT NOT NULL,

    UsedVersion INTEGER NOT NULL REFERENCES ContentVersion(Id) ON DELETE RESTRICT
);
