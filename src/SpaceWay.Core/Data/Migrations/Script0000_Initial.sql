CREATE TABLE Config(
    Key   TEXT NOT NULL PRIMARY KEY,
    Value TEXT NOT NULL
);

CREATE TABLE AuthServer(
    Id          TEXT NOT NULL PRIMARY KEY,
    DisplayName TEXT NOT NULL,
    Address     TEXT NOT NULL
);

CREATE TABLE Account(
    UserId       TEXT NOT NULL,
    AuthServerId TEXT NOT NULL REFERENCES AuthServer(Id) ON DELETE CASCADE,
    Username     TEXT NOT NULL,
    TokenExpiry  TEXT NOT NULL,

    PRIMARY KEY (UserId, AuthServerId)
);

CREATE TABLE Hub(
    Id          TEXT NOT NULL PRIMARY KEY,
    DisplayName TEXT NOT NULL,
    Address     TEXT NOT NULL,
    Priority    INTEGER NOT NULL,
    Enabled     INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE ModProfile(
    Id   TEXT NOT NULL PRIMARY KEY,
    Name TEXT NOT NULL,
    Note TEXT
);

CREATE TABLE ModProfileAssembly(
    ProfileId TEXT NOT NULL REFERENCES ModProfile(Id) ON DELETE CASCADE,
    FileName  TEXT NOT NULL,
    SortOrder INTEGER NOT NULL,

    PRIMARY KEY (ProfileId, FileName)
);

CREATE TABLE FavoriteFolder(
    Id           TEXT NOT NULL PRIMARY KEY,
    Name         TEXT NOT NULL,
    ParentId     TEXT REFERENCES FavoriteFolder(Id) ON DELETE SET NULL,
    SortOrder    INTEGER NOT NULL DEFAULT 0,
    ModProfileId TEXT REFERENCES ModProfile(Id) ON DELETE SET NULL
);

CREATE TABLE FavoriteServer(
    Id           TEXT NOT NULL PRIMARY KEY,
    Address      TEXT NOT NULL,

    AddressKey   TEXT NOT NULL UNIQUE,

    ReportedName TEXT,
    CustomName   TEXT,
    Note         TEXT,
    FolderId     TEXT REFERENCES FavoriteFolder(Id) ON DELETE SET NULL,
    SortOrder    INTEGER NOT NULL DEFAULT 0,

    ModProfileId TEXT REFERENCES ModProfile(Id) ON DELETE SET NULL,

    ModsDisabled INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE FavoriteTag(
    ServerId TEXT NOT NULL REFERENCES FavoriteServer(Id) ON DELETE CASCADE,
    Tag      TEXT NOT NULL,

    PRIMARY KEY (ServerId, Tag)
);

CREATE INDEX IX_FavoriteServer_Folder ON FavoriteServer(FolderId);
CREATE INDEX IX_FavoriteFolder_Parent ON FavoriteFolder(ParentId);
CREATE INDEX IX_Account_AuthServer ON Account(AuthServerId);
