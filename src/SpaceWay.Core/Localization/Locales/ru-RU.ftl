## Общие
app-name = SpaceWay Launcher

## Навигация
nav-servers = Серверы
nav-favorites = Избранное
nav-accounts = Аккаунты
nav-mods = Моды
nav-settings = Настройки
sidebar-account = Аккаунт

## Список серверов
servers-empty = Серверы не найдены
servers-refresh = Обновить
servers-loading = Загружаем список серверов…
servers-hub-failed = { $count ->
    [one] { $count } хаб недоступен
    [few] { $count } хаба недоступны
   *[other] { $count } хабов недоступны
}
servers-source = Источник: { $hubs }
servers-count = { $count ->
    [one] { $count } сервер
    [few] { $count } сервера
   *[other] { $count } серверов
}

## Аккаунты
accounts-empty = Аккаунты не добавлены

## Настройки
settings-language = Язык

## Карточка сервера
server-slots = { $players } / { $max }
server-slots-unlimited = { $players }
server-round-lobby = Лобби
server-round-ending = Раунд закончен
server-round-hours = Раунд: { $hours } ч { $minutes } мин
server-round-minutes = Раунд: { $minutes } мин
server-details-loading = Загружаем описание сервера…
server-details-empty = Сервер не указал описание и ссылки

server-offline = Не в сети

## Фильтры
filter-search-placeholder = Поиск по названию или адресу
filter-hide-full = Скрыть полные
filter-hide-empty = Скрыть пустые
filter-hide-adult = Скрыть 18+
filter-language = Язык
filter-language-any = Любой
filter-reset = Сбросить

## Сортировка
sort-players = По игрокам
sort-name = По названию
sort-round-time = По времени раунда
sort-occupancy = По заполненности

## Избранное
favorites-empty = В избранном пока пусто
favorites-hint = Добавляйте серверы звёздочкой на карточке
favorites-hint-manual = Или добавьте сервер по адресу кнопкой выше
favorites-add = Добавить сервер
favorites-add-title = Добавить сервер по адресу
favorites-add-address = Адрес
favorites-add-address-hint = Например: example.com:1212. Префикс ss14:// необязателен
favorites-add-name = Название (необязательно)
favorites-add-submit = Добавить
favorites-add-bad-address = Не похоже на адрес сервера
favorites-add-duplicate = Этот сервер уже в избранном

## Аккаунты
accounts-username = Логин
accounts-password = Пароль
accounts-tfa-code = Код двухфакторной аутентификации
accounts-login = Войти
accounts-logout = Выйти
accounts-select = Выбрать
accounts-unknown-server = Неизвестный сервер
accounts-token-valid = Действует ещё { $days ->
    [one] { $days } день
    [few] { $days } дня
   *[other] { $days } дней
}
accounts-token-expired = Сессия истекла
accounts-insecure-storage = Системное хранилище паролей недоступно, поэтому токены хранятся в обычном файле

## Ошибки входа
auth-error-credentials = Неверный логин или пароль
auth-error-unconfirmed = Почта аккаунта не подтверждена
auth-error-tfa-required = Введите код двухфакторной аутентификации
auth-error-tfa-invalid = Неверный код двухфакторной аутентификации
auth-error-locked = Аккаунт заблокирован
auth-error-connection = Не удалось связаться с сервером авторизации
auth-error-unknown = Не удалось войти

## Добавление аккаунта
add-account-title = Новый аккаунт
add-account-offline-title = Игра без авторизации
add-account-kind-offline = Без авторизации
add-account-kind-offline-hint = Вход только по имени. Работает на серверах, которые это разрешают
add-account-continue = Продолжить
add-account-back = Назад
add-account-offline-warning = Имя не проверяется, поэтому некоторые серверы могут вас не пустить
accounts-new = Новый аккаунт
accounts-offline-badge = Без авторизации

## Предложение войти
sign-in-prompt-title = Войдите, чтобы играть
sign-in-prompt-text = Большинство серверов пускает только игроков с аккаунтом Space Station 14. Войдите один раз — лаунчер вас запомнит.
sign-in-prompt-offline = Нет аккаунта? Некоторые серверы пускают и без него — на следующем шаге выберите «Без авторизации».
sign-in-prompt-sign-in = Войти
sign-in-prompt-later = Позже

## Реплеи и контент-бандлы
bundle-title = Запуск из файла
bundle-open = Открыть реплей
bundle-picker-title = Реплей или контент-бандл
bundle-picker-type = Реплеи и бандлы SS14
bundle-drop-hint = Отпустите, чтобы запустить реплей

## Имя игрока
username-rules = Латинские буквы, цифры и подчёркивание
username-too-short = Не короче { $min } символов
username-too-long = Не длиннее { $max } символов
username-invalid-characters = Можно только латинские буквы, цифры и подчёркивание

## Хабы
nav-hubs = Хабы
hubs-drag-hint = Порядок меняется перетаскиванием карточек
hubs-add = Добавить хаб
hubs-add-title = Новый хаб
hubs-name = Название
hubs-address = Адрес
hubs-remove = Удалить
hubs-priority = Приоритет { $position }
hubs-empty = Нет ни одного хаба, поэтому список серверов пуст
hubs-all-disabled = Все хабы отключены, поэтому список серверов пуст
hubs-hint = Если сервер есть в нескольких хабах, показываются данные хаба с более высоким приоритетом
servers-count-filtered = Показано { $count }, скрыто фильтрами { $hidden }
servers-empty-filtered = Фильтры скрыли { $hidden ->
    [one] { $hidden } сервер
    [few] все { $hidden } сервера
   *[other] все { $hidden } серверов
}

## Подключение к серверу
connect-play = Играть
connect-title = Подключение
connect-cancel = Отменить
connect-close = Закрыть
connect-failed = Подключиться не удалось
connect-cancelled = Подключение отменено
connect-without-mods = Запустить без модов
connect-sign-in = Войти
connect-mods = { $count ->
    [one] Применяется { $count } мод
    [few] Применяется { $count } мода
   *[other] Применяется { $count } модов
}
mods-enabled-count = { $count ->
    [0] Ни один мод не включён
    [one] Включён { $count } мод
    [few] Включено { $count } мода
   *[other] Включено { $count } модов
}
mods-open-folder = Открыть папку модов
mods-refresh = Обновить
mods-empty = Модов пока нет
mods-empty-hint = Перетащите сюда файлы модов (.dll) или положите их в папку модов и нажмите «Обновить»
mods-missing = Файл не найден в папке модов
mods-bad-name = Игра не загрузит этот мод: имя файла должно начинаться с { $prefix }
mods-warning = Включённые моды работают на всех серверах. Мод, собранный под другую сборку игры, может вызвать вылет при запуске — тогда лаунчер предложит запустить игру без модов
mods-drop-hint = Отпустите, чтобы добавить моды
mods-import-added = Добавлен и включён: { $file }
mods-import-replaced = Заменён: { $file }
mods-import-already = Уже добавлен: { $file }
mods-import-duplicate = { $file } совпадает с уже добавленным { $existing }, поэтому не добавлен
mods-import-skipped = Замена отменена: { $file }
mods-import-not-assembly = Пропущен, это не мод (.dll): { $file }
mods-import-bad-name = Игра не загрузит { $file }: имя файла должно начинаться с { $prefix }
mods-replace-title = Заменить мод?
mods-replace-text = В папке модов уже есть { $file } с другим содержимым — вероятно, другая версия. Заменить? Включён мод или нет, не изменится.
mods-replace-confirm = Заменить
connect-mod-rejected = Игра заблокировала мод { $assembly }: он использует запрещённый тип { $type }
connect-mod-rejected-type = Игра заблокировала мод: он использует запрещённый тип { $type }
connect-mod-rejected-assembly = Игра заблокировала мод { $assembly }
connect-progress-files = { $done } из { $total }
connect-progress-bytes = { $done } из { $total }

## Этапы подключения
stage-asking-server = Связываемся с сервером
stage-opening-bundle = Открываем файл
stage-checking-version = Проверяем, что уже скачано
stage-fetching-manifest = Получаем список файлов
stage-downloading-files = Скачиваем файлы игры
stage-downloading-zip = Скачиваем контент
stage-storing-files = Сохраняем файлы
stage-downloading-engine = Скачиваем движок
stage-downloading-modules = Скачиваем модули движка
stage-committing = Сохраняем
stage-culling = Удаляем устаревшие файлы
stage-starting-game = Запускаем игру
stage-done = Готово

## Единицы измерения
unit-bytes = Б
unit-kib = КиБ
unit-mib = МиБ
unit-gib = ГиБ

## Политика приватности сервера
privacy-title = Политика приватности сервера
privacy-explanation = Сервер { $server } просит принять его политику приватности. В ней описано, какие данные о вас он собирает
privacy-changed = Сервер { $server } обновил политику приватности с тех пор, как вы её приняли
privacy-open = Прочитать политику
privacy-accept = Согласен
privacy-decline = Отказаться
privacy-decline-hint = Без согласия подключиться к этому серверу нельзя. На другие серверы это не повлияет

## Ошибки подключения и скачивания
error-server-unreachable = Сервер не отвечает или вернул некорректный ответ
error-bad-server-address = Некорректный адрес сервера: { $address }
error-no-build-info = Сервер не сообщил информацию о своей сборке
error-auth-login-failed = Сервер требует авторизацию, но войти в аккаунт не удалось
error-auth-no-account = Сервер требует авторизацию, но аккаунт не выбран
error-privacy-nobody-to-ask = Сервер требует согласия с политикой приватности, но запросить его не удалось
error-privacy-declined = Без согласия с политикой приватности подключиться к серверу нельзя
error-loader-missing = Не найден загрузчик игры: { $path }. Попробуйте переустановить лаунчер
error-loader-start-failed = Не удалось запустить загрузчик игры
error-bad-connect-address = Сервер указал некорректный адрес подключения: { $address }
error-no-content-source = Сервер не указал, откуда скачивать контент
error-manifest-fetch-failed = Не удалось получить список файлов с сервера: { $address }
error-manifest-format = Сервер прислал манифест неизвестного формата
error-manifest-line = Манифест сервера повреждён
error-manifest-hash = Хеш манифеста не совпадает с указанным сервером
error-files-fetch-failed = Не удалось скачать файлы игры с сервера: { $address }
error-file-negative-length = Сервер прислал файл с некорректной длиной
error-file-wrong-size = Размер распакованного файла не совпадает с ожидаемым
error-file-corrupt = Файл { $path } скачался с ошибками
error-file-missing-after-download = Файл { $path } не найден после скачивания
error-protocol-check-failed = Сервер не ответил на проверку протокола загрузки: { $address }
error-protocol-unknown = Сервер не сообщил, какие версии протокола загрузки он поддерживает
error-protocol-mismatch = Сервер поддерживает протокол загрузки версий { $min }–{ $max }, а лаунчер — { $ours }
error-zip-fetch-failed = Не удалось скачать контент с сервера: { $address }
error-zip-hash = Хеш архива с контентом не совпадает с указанным сервером
error-unknown-compression = Неизвестный способ сжатия в базе контента: { $kind }
error-engine-version-unknown = Версии движка { $version } нет в манифесте
error-engine-redirect-loop = Перенаправления версий движка зациклились на { $version }
error-engine-insecure = Версия движка { $version } отозвана как небезопасная
error-engine-no-platform = Сборки движка { $version } для { $platform } нет
error-engine-hash-mismatch = Контрольная сумма скачанного движка { $version } не совпадает
error-engine-signature-mismatch = Подпись скачанного движка { $version } не прошла проверку
error-module-insecure = Версия модуля { $module } { $version } отозвана как небезопасная
error-module-no-platform = Сборки модуля { $module } для { $platform } нет
error-module-hash-mismatch = Контрольная сумма скачанного модуля { $module } не совпадает
error-module-signature-mismatch = Подпись скачанного модуля { $module } не прошла проверку

## Серверы авторизации
accounts-servers = Серверы авторизации
accounts-server-remove = Удалить
accounts-server-accounts = { $count ->
    [0] без аккаунтов
    [one] { $count } аккаунт
    [few] { $count } аккаунта
   *[other] { $count } аккаунтов
}
accounts-server-remove-title = Удалить сервер авторизации
accounts-server-remove-text = { $count ->
    [0] Сервер { $server } будет удалён из списка. Аккаунтов на нём нет
    [one] Вместе с сервером { $server } будет удалён { $count } аккаунт. Чтобы вернуть его, придётся снова ввести пароль
    [few] Вместе с сервером { $server } будут удалены { $count } аккаунта. Чтобы вернуть их, придётся снова ввести пароль
   *[other] Вместе с сервером { $server } будут удалены { $count } аккаунтов. Чтобы вернуть их, придётся снова ввести пароль
}
accounts-server-remove-confirm = Удалить
confirm-decline = Отмена

## Раздел настроек
settings-look = Вид
settings-language-hint = Язык меняется сразу, без перезапуска
settings-game = Игра
settings-compat = Режим совместимости
settings-compat-hint = Старый режим отрисовки. Включите, если игра не запускается или показывает артефакты на старой видеокарте
settings-data = Данные
settings-user-data = Настройки, аккаунты и избранное
settings-downloads = Скачанное
settings-content = Контент серверов
settings-content-hint = Файлы игры со всех серверов, на которые вы заходили
settings-engines = Движки
settings-engines-hint = Сборки Robust и его модули
settings-open-folder = Открыть папку
settings-clear = Очистить
settings-clearing = Удаляем…
settings-clear-content-title = Очистить контент серверов
settings-clear-content-text = Будет освобождено { $size }. Файлы игры скачаются заново при следующем входе на каждый сервер
settings-clear-engines-title = Очистить движки
settings-clear-engines-text = Будет освобождено { $size }. Нужная версия движка скачается заново при следующем запуске игры
settings-clear-confirm = Очистить
error-clear-while-playing = Сейчас запущена игра, которая использует эти файлы. Закройте её и попробуйте снова
error-mod-file-missing = Файл мода { $file } не найден в папке модов
error-mod-bad-name = Игра загружает только моды, имя которых начинается с { $prefix }, а { $file } назван иначе
error-mod-import-failed = Не удалось добавить { $file }: { $reason }
error-not-a-bundle = { $file } не является реплеем или контент-бандлом: внутри нет rt_content_bundle.json
error-bundle-unreadable = Не удалось прочитать { $file }: это не zip-архив, или файл повреждён
error-bundle-bad-metadata = Не удалось прочитать rt_content_bundle.json: файл повреждён
error-bundle-base-unavailable = Не удалось скачать сборку, на которой записан реплей: { $fork } { $version }. Вероятно, она больше недоступна — старые сборки со временем удаляются. Также проверьте подключение к интернету

## Добавление сервера авторизации
auth-server-add = Добавить
auth-server-add-title = Новый сервер авторизации
auth-server-add-hint = Нужен для игровых серверов с собственной авторизацией. После добавления в него можно войти через «Новый аккаунт».
auth-server-name = Название
auth-server-address = Адрес
auth-server-address-hint = Например: auth.example.com. Если не указано иное, используется https
auth-server-address-invalid = Это не похоже на адрес сервера авторизации
auth-server-address-insecure = По http пароль передаётся без шифрования. Используйте https — http допустим только для localhost
auth-server-duplicate = Этот сервер авторизации уже добавлен
auth-server-add-submit = Добавить

## Обновления лаунчера
settings-updates = Обновления
settings-updates-current = Текущая версия: { $version }
settings-updates-check = Проверять обновления при запуске
settings-updates-check-now = Проверить сейчас
update-available = Доступна версия { $version }
update-ready = Версия { $version } загружена. Закройте лаунчер, чтобы установить её
update-whats-new = Что нового
update-download = Скачать
update-status-checking = Проверяем обновления…
update-status-up-to-date = Установлена последняя версия
update-status-downloading = Загружаем версию { $version }…
update-status-failed = Не удалось проверить обновления
update-error-no-checksum = В релизе нет контрольной суммы установщика, поэтому он не загружен
update-error-checksum-mismatch = Загруженный установщик повреждён или подменён, поэтому он удалён
