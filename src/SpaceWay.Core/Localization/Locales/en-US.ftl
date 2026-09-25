## General
app-name = SpaceWay Launcher

## Navigation
nav-servers = Servers
nav-favorites = Favorites
nav-accounts = Accounts
nav-mods = Mods
nav-settings = Settings
sidebar-account = Account

## Server list
servers-empty = No servers found
servers-refresh = Refresh
servers-loading = Loading server list…
servers-hub-failed = { $count ->
    [one] { $count } hub is unavailable
   *[other] { $count } hubs are unavailable
}
servers-source = From: { $hubs }
servers-count = { $count ->
    [one] { $count } server
   *[other] { $count } servers
}

## Accounts
accounts-empty = No accounts added

## Settings
settings-language = Language

## Server card
server-slots = { $players } / { $max }
server-slots-unlimited = { $players }
server-round-lobby = Lobby
server-round-ending = Round over
server-round-hours = Round: { $hours }h { $minutes }m
server-round-minutes = Round: { $minutes }m
server-details-loading = Loading server info…
server-details-empty = The server has no description or links.

server-offline = Offline

## Filters
filter-search-placeholder = Search by name or address
filter-hide-full = Hide full
filter-hide-empty = Hide empty
filter-hide-adult = Hide 18+
filter-language = Language
filter-language-any = Any
filter-reset = Reset

## Sorting
sort-players = By players
sort-name = By name
sort-round-time = By round time
sort-occupancy = By occupancy

## Favorites
favorites-empty = Nothing in favorites yet
favorites-hint = Add servers with the star on their card
favorites-hint-manual = Or add a server by address with the button above
favorites-add = Add server
favorites-add-title = Server by address
favorites-add-address = Address
favorites-add-address-hint = For example: example.com:1212. The ss14:// prefix is optional
favorites-add-name = Name (optional)
favorites-add-submit = Add
favorites-add-bad-address = This does not look like a server address
favorites-add-duplicate = That server is already in favorites

## Accounts
accounts-username = Username
accounts-password = Password
accounts-tfa-code = Two-factor code
accounts-login = Log in
accounts-logout = Log out
accounts-select = Use
accounts-unknown-server = Unknown server
accounts-token-valid = Valid for { $days ->
    [one] { $days } day
   *[other] { $days } days
}
accounts-token-expired = Session expired
accounts-insecure-storage = System keystore is unavailable — tokens are stored in a plain file

## Sign-in errors
auth-error-credentials = Wrong username or password
auth-error-unconfirmed = Account email is not confirmed
auth-error-tfa-required = Enter your two-factor code
auth-error-tfa-invalid = Two-factor code is wrong
auth-error-locked = Account is locked
auth-error-connection = Cannot reach the auth server
auth-error-unknown = Login failed

## Adding an account
add-account-title = New account
add-account-offline-title = Play without login
add-account-kind-offline = Without authentication
add-account-kind-offline-hint = Name only. Works on servers that allow it
add-account-continue = Continue
add-account-back = Back
add-account-offline-warning = Your name will not be verified. Servers may refuse to let you in
accounts-new = New account
accounts-offline-badge = No login

## Sign-in prompt
sign-in-prompt-title = Sign in to play
sign-in-prompt-text = Most servers only let in players with a Space Station 14 account. Sign in once — the launcher will remember you.
sign-in-prompt-offline = No account? Some servers allow playing with just a name: choose "Without authentication" in the next step.
sign-in-prompt-sign-in = Sign in
sign-in-prompt-later = Later

## Replays and content bundles
bundle-title = Launch from file
bundle-open = Open replay
bundle-picker-title = Replay or content bundle
bundle-picker-type = SS14 replays and bundles
bundle-drop-hint = Drop to launch the replay

## Username
username-rules = Latin letters, digits and underscore only
username-too-short = At least { $min } characters
username-too-long = No more than { $max } characters
username-invalid-characters = Only latin letters, digits and underscore are allowed

## Hubs
nav-hubs = Hubs
hubs-drag-hint = Drag cards to change the order
hubs-add = Add hub
hubs-add-title = New hub
hubs-name = Name
hubs-address = Address
hubs-remove = Remove
hubs-priority = Priority { $position }
hubs-empty = No hubs. The server list will stay empty
hubs-all-disabled = All hubs are disabled — the server list will stay empty
hubs-hint = Priority decides whose data wins when several hubs advertise the same server
servers-count-filtered = Showing { $count }, { $hidden } hidden by filters
servers-empty-filtered = Filters hid { $hidden ->
    [one] { $hidden } server
   *[other] all { $hidden } servers
}

## Connecting to a server
connect-play = Play
connect-title = Connecting
connect-cancel = Cancel
connect-close = Close
connect-failed = Could not connect
connect-cancelled = Connection cancelled
connect-without-mods = Start without mods
connect-sign-in = Sign in
connect-mods = { $count ->
    [one] { $count } mod applied
   *[other] { $count } mods applied
}
mods-enabled-count = { $count ->
    [0] No mods enabled
    [one] { $count } mod enabled
   *[other] { $count } mods enabled
}
mods-open-folder = Open mods folder
mods-refresh = Reload
mods-empty = No mods
mods-empty-hint = Drag assemblies here, or put them into the mods folder and press Reload
mods-missing = The file is gone from the folder
mods-bad-name = The engine will not take it: the name must start with { $prefix }
mods-warning = Enabled mods apply on every server. A mod built for another fork can crash the game on startup — the launcher will then offer to join without mods
mods-drop-hint = Drop to add to mods
mods-import-added = Added and enabled: { $file }
mods-import-replaced = Replaced: { $file }
mods-import-already = Already there, nothing changed: { $file }
mods-import-duplicate = { $file } is the same mod as { $existing }. A second copy would crash the game, not copied
mods-import-skipped = Kept the old one: { $file }
mods-import-not-assembly = Not an assembly, skipped: { $file }
mods-import-bad-name = The engine won't load { $file }: the name must start with { $prefix }
mods-replace-title = Replace mod?
mods-replace-text = The mods folder already has { $file }, but with different contents — most likely another version. Replace it with this file? Whether the mod is enabled won't change.
mods-replace-confirm = Replace
connect-mod-rejected = The engine sandbox refused the mod { $assembly }: it uses { $type }, which the engine forbids
connect-mod-rejected-type = The engine sandbox refused the mod: it uses { $type }, which the engine forbids
connect-mod-rejected-assembly = The engine sandbox refused the mod { $assembly }
connect-progress-files = { $done } of { $total }
connect-progress-bytes = { $done } of { $total }

## Connection stages
stage-asking-server = Asking the server
stage-opening-bundle = Opening the file
stage-checking-version = Checking what is already downloaded
stage-fetching-manifest = Fetching the file list
stage-downloading-files = Downloading game files
stage-downloading-zip = Downloading content
stage-storing-files = Storing files
stage-downloading-engine = Downloading the engine
stage-downloading-modules = Downloading engine modules
stage-committing = Saving
stage-culling = Cleaning up
stage-starting-game = Starting the game
stage-done = Done

## Units
unit-bytes = B
unit-kib = KiB
unit-mib = MiB
unit-gib = GiB

## Server privacy policy
privacy-title = Server privacy policy
privacy-explanation = { $server } asks you to agree to what data it collects about you
privacy-changed = The terms of { $server } have changed since you agreed to them
privacy-open = Read the policy
privacy-accept = I agree
privacy-decline = Decline
privacy-decline-hint = Without agreement you cannot connect to this server. Other servers are not affected

## Connection and download errors
error-server-unreachable = The server does not answer, or answers with nonsense
error-bad-server-address = Not a server address: { $address }
error-no-build-info = The server did not say which build it runs
error-auth-login-failed = The server requires an account, and signing in failed
error-auth-no-account = The server requires an account, and none is selected
error-privacy-nobody-to-ask = The server asks to accept its privacy policy, and there is nobody to ask
error-privacy-declined = Without accepting the privacy policy this server cannot be joined
error-loader-missing = The game loader is missing next to the launcher: { $path }
error-loader-start-failed = Could not start the game loader
error-bad-connect-address = The server gave a connect address we cannot read: { $address }
error-no-content-source = The server did not say where to get its content
error-manifest-fetch-failed = Could not get the file list from the server: { $address }
error-manifest-format = The server sent a manifest in an unknown format
error-manifest-line = A broken line in the server manifest
error-manifest-hash = The manifest hash does not match the one the server declared
error-files-fetch-failed = The server did not serve the game files: { $address }
error-file-negative-length = The server sent a file of negative length
error-file-wrong-size = The unpacked file turned out to be the wrong size
error-file-corrupt = File { $path } arrived corrupted
error-file-missing-after-download = File { $path } is not in the database although it should have downloaded
error-protocol-check-failed = The server does not answer the resumable-download request: { $address }
error-protocol-unknown = The server did not say which download protocol it speaks
error-protocol-mismatch = The server supports download protocol versions { $min }–{ $max }, the launcher supports { $ours }
error-zip-fetch-failed = Could not download the content from the server: { $address }
error-zip-hash = The content archive hash does not match the one the server declared
error-unknown-compression = Unknown compression in the content database: { $kind }
error-engine-version-unknown = Engine version { $version } is not in the manifest
error-engine-redirect-loop = Engine version redirects loop around { $version }
error-engine-insecure = Engine version { $version } was revoked as insecure
error-engine-no-platform = There is no { $version } engine build for { $platform }
error-engine-hash-mismatch = The checksum of the downloaded engine { $version } does not match
error-engine-signature-mismatch = The signature of the downloaded engine { $version } does not match
error-module-insecure = Module { $module } { $version } was revoked as insecure
error-module-no-platform = There is no { $module } module build for { $platform }
error-module-hash-mismatch = The checksum of the downloaded module { $module } does not match
error-module-signature-mismatch = The signature of the downloaded module { $module } does not match

## Auth servers
accounts-servers = Auth servers
accounts-server-remove = Remove
accounts-server-accounts = { $count ->
    [0] no accounts
    [one] { $count } account
   *[other] { $count } accounts
}
accounts-server-remove-title = Remove auth server
accounts-server-remove-text = { $count ->
    [0] The server { $server } will disappear from the list. It has no accounts
    [one] Removing { $server } also removes { $count } account and its token. Signing back in will need the password
   *[other] Removing { $server } also removes { $count } accounts and their tokens. Signing back in will need the password
}
accounts-server-remove-confirm = Remove
confirm-decline = Cancel

## Settings section
settings-look = Appearance
settings-language-hint = The interface switches at once, no restart needed
settings-game = Game
settings-compat = Compatibility mode
settings-compat-hint = The old way of drawing. Turn it on if the game does not start or renders garbage on an old graphics card
settings-data = Data
settings-user-data = Settings, accounts and favorites
settings-downloads = Downloads
settings-content = Server content
settings-content-hint = Game files from every server you have visited
settings-engines = Engines
settings-engines-hint = Robust builds and its modules
settings-open-folder = Open folder
settings-clear = Clear
settings-clearing = Clearing…
settings-clear-content-title = Clear server content
settings-clear-content-text = This frees { $size }. Game files will download again on the next visit to each server
settings-clear-engines-title = Clear engines
settings-clear-engines-text = This frees { $size }. The engine build you need will download again on the next launch
settings-clear-confirm = Clear
error-clear-while-playing = The game is running right now and reads files from the same database. Close it and try again
error-mod-file-missing = Assembly { $file } is not in the mods folder
error-mod-bad-name = The engine only takes assemblies named { $prefix }something, and { $file } is named otherwise
error-mod-import-failed = Could not add { $file }: { $reason }
error-not-a-bundle = { $file } is neither a replay nor a content bundle: there is no rt_content_bundle.json inside
error-bundle-unreadable = Could not read { $file }: it is not a zip archive or it is damaged
error-bundle-bad-metadata = The bundle description is damaged: could not parse rt_content_bundle.json
error-bundle-base-unavailable = Could not download the build this replay was recorded on: { $fork } { $version }. It is most likely no longer hosted, as old builds are removed over time. Also check your internet connection

## Adding an auth server
auth-server-add = Add
auth-server-add-title = New auth server
auth-server-add-hint = For game servers that run their own authentication. Once added, you can sign in to it from the New account dialog.
auth-server-name = Name
auth-server-address = Address
auth-server-address-hint = For example: auth.example.com. https is used unless specified otherwise
auth-server-address-invalid = This does not look like an auth server address
auth-server-address-insecure = Plain http would send your password unencrypted. Use https, or http only for localhost
auth-server-duplicate = This auth server has already been added
auth-server-add-submit = Add

## Launcher updates
settings-updates = Updates
settings-updates-current = Current version: { $version }
settings-updates-check = Check for updates on startup
settings-updates-check-now = Check now
update-available = Version { $version } is available
update-ready = Version { $version } is ready. Close the launcher to install it
update-whats-new = What's new
update-download = Download
update-status-checking = Checking for updates…
update-status-up-to-date = You have the latest version
update-status-downloading = Downloading version { $version }…
update-status-failed = Could not check for updates
update-error-no-checksum = The release has no checksum for the installer, so it was not downloaded
update-error-checksum-mismatch = The downloaded installer is damaged or was tampered with, so it was discarded
