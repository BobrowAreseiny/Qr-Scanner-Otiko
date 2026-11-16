# Release-сборка и перенос старой базы

Дата: 16.11.2026.

## Параметры приложения

| Параметр | Значение |
|---|---|
| Package id | `by.otiko.qrscanner` |
| Название | `Сканер QR` |
| Версия | `1.0.0` (`versionCode` 1) |
| minSdk | 21 (Android 5+) |
| targetSdk | 36 |
| ABI | `armeabi-v7a`, `arm64-v8a`, `x86_64` |

Старое приложение `com.companyname.qrscanner` остаётся установленным. Новое приложение ставится рядом, данные старого приложения не затрагиваются.

## Release-подпись

Keystore создан отдельно от Git:

```text
C:\Users\Bobro\source\repos\QRScanner-Maui-secrets\qrscanner-release.keystore
```

Параметры:

```text
alias:   qrscanner
key:     RSA 4096
SHA-256: fdf1ed291a91790be6a618bc2fb5badbfebe2a36640b6a6d784e17167e0cbb89
SHA-1:   f6fac153dd2bf07d09d629ba1747da139a0c373a
```

Пароль хранится рядом с keystore в файле `keystore-password.txt`. Пароль и keystore нельзя коммитить, публиковать или передавать в чат.

Файлы подписи:

- `QRScanner-Maui-secrets\signing.props` — параметры Android-подписи;
- `signing.local.props` в корне репозитория — импортирует `signing.props` и исключён из Git.

`QRScanner.App.csproj` подключает `signing.local.props`, только если файл существует. Без него Release-сборка подписывается отладочным ключом.

## Сборка Release

```powershell
dotnet build src/QRScanner.App/QRScanner.App.csproj `
  -c Release -f net10.0-android `
  -p:EmbedAssembliesIntoApk=true `
  -p:AndroidPackageFormat=apk
```

APK:

```text
src/QRScanner.App/bin/Release/net10.0-android/by.otiko.qrscanner-Signed.apk
```

Проверка подписи и package id:

```powershell
& "C:\Program Files (x86)\Android\android-sdk\build-tools\36.0.0\apksigner.bat" `
  verify --print-certs `
  src/QRScanner.App/bin/Release/net10.0-android/by.otiko.qrscanner-Signed.apk

& "C:\Program Files (x86)\Android\android-sdk\build-tools\36.0.0\aapt.exe" `
  dump badging `
  src/QRScanner.App/bin/Release/net10.0-android/by.otiko.qrscanner-Signed.apk
```

## Обход падения Release в .NET MAUI 10

В Release-конфигурации включён обход известной ошибки .NET MAUI 10:

```xml
<AndroidEnableMarshalMethods>false</AndroidEnableMarshalMethods>
```

Без него приложение падает при старте:

```text
java.lang.UnsatisfiedLinkError:
No implementation found for void ...MauiApplication.n_onCreate()
```

После включения обхода Release-APK запускается на ATOL Smart.Lite (Android 11, arm64-v8a).

## Защита данных

В `AndroidManifest.xml`:

```xml
<application android:allowBackup="false" ... />
```

Автоматическое резервное копирование Android отключено. Резервные копии приложения создаются вручную и через `DatabaseBackupService`; ключ базы хранится в `SecureStorage`.

## Перенос старой базы

Старая база `barcode.db` не зашифрована. Новое приложение использует SQLCipher, поэтому файл не копируется напрямую, а импортируется.

Путь на ТСД:

```text
/data/user/0/com.companyname.qrscanner/files/.local/share/barcode.db
```

Старое приложение отладочное, файл можно забрать через `run-as`:

```powershell
adb -s 001D1105979947 exec-out run-as com.companyname.qrscanner `
  cat files/.local/share/barcode.db > barcode.db
```

Для бинарного вывода в PowerShell использовать `cmd /c`, иначе файл будет повреждён перенаправлением `>`.

### Импорт в приложении

1. Скопировать `barcode.db` на ТСД, например в `Download`.
2. Открыть «Настройки» → «Перенос старой базы».
3. Нажать «Импортировать старую базу».
4. Выбрать `barcode.db`.
5. Подтвердить импорт.

Импорт выполняется только в пустую базу. Если в новой базе уже есть короба, приложение покажет сообщение и не откроет выбор файла. Временная копия файла в `cache/imports` удаляется после импорта.

### Соответствие полей

| Старая таблица | Поле | Новое поле |
|---|---|---|
| `CorobSQL` | `CorobName` | `Corob.Name` |
| `CorobSQL` | `IsScanned` | `Corob.IsShipped` |
| `CorobSQL` | `BoxBarcodes` (BLOB) | не импортируется, если пустой |
| `BoxBarcodeSQL` | `CorobID` | `BoxBarcode.CorobId` |
| `BoxBarcodeSQL` | `BoxBarcode` | `BoxBarcode.Barcode` |
| `BoxBarcodeSQL` | `BarcodeNumber` | `BoxBarcode.Gtin` |
| `BoxBarcodeSQL` | `BarcodeVendor` | `BoxBarcode.VendorCode` |
| `BoxBarcodeSQL` | `BarcodeSize` | `BoxBarcode.Size` |

Дополнительно:

- `Capacity = 0` — стандартный режим без ограничения количества;
- `CreatedAtUtc` и `ScannedAtUtc` — время импорта, в старой базе дат нет;
- `IsScanned` в старом приложении выставляется при сохранении отгрузки (`OrderViewModel.SaveCorobData`), поэтому соответствует `IsShipped`.

### Проверено на ТСД

Копия `Smart_Prime_001D1105979947`:

- 5 коробов;
- 429 марок;
- 0 марок без короба;
- 0 пустых кодов;
- 0 дублей полных кодов марок;
- короба: 63, 80, 87, 95, 104 марки;
- после импорта отображаются как «Отгружен», количество показывается как «63 марок» и т.д.;
- повторный импорт в непустую базу заблокирован;
- временный файл удалён.

## Проверка API на ТСД

- Сервер: `https://api.datamark.by/`.
- Логин и пароль введены вручную на ТСД и сохранены в `SecureStorage`.
- «Проверить подключение»: **«Подключение к Электронному знаку (Datamark) установлено.»**
- GTIN `04810498044822`: найден.
- Карточка товара: «Полуботинки женские», артикул `1549`, размер `37`, GTIN подтверждён, группа `shoes`, «Наш товар: Да».
- Добавление марки: в тестовый короб добавлен полный код с GTIN `04810498044822`; API вернул артикул `1549` и размер `37`, марка сохранилась. Тестовый короб удалён.
- Примечание: «голый» GTIN без префикса `01` добавляется как код без запроса карточки товара. Реальные DataMatrix-коды содержат `01` и обрабатываются с карточкой.

Осталось проверить на ТСД:

- физическое сканирование с аппаратного сканера;
- клавиши 1/2/0 на всех экранах;
- QR-код, «Поделиться» и сохранение PNG;
- резервное копирование в Google Drive и восстановление БД;
- обмен с 1С.

## Отображение марок

В начале старых кодов встречается символ `è` — это служебный маркер GS1/FNC1, который добавляет сканер. Он не является частью GTIN или кода товара.

- в интерфейсе показываются GTIN, артикул и размер;
- полный код марки в списке не выводится;
- при экспорте `.txt` служебный префикс `è` удаляется;
- старый формат `.txt` сохраняется: одна марка на строку, UTF-8 без BOM.

## Экспорт заказа

- Файл `.txt` сохраняется в общую папку `Download/Otiko` — её видно в проводнике Android и по USB.
- Если запись в Downloads недоступна, файл сохраняется во внутреннюю папку приложения.
- Формат: одна полная марка на строку, GS `0x1D` сохраняется, `è`/`е` удаляются, UTF-8 без BOM, LF.
- В настройках Datamark и 1С (Basic) есть переключатель «Показать пароль».

## CI

Workflow `.github/workflows/android.yml` собирает Release APK с package id `com.companyname.qrscanner.ci` и подписывает его отладочным ключом. Это проверочный артефакт CI.

Для release-подписи в CI нужно добавить GitHub Secrets:

- `KEYSTORE_BASE64` — keystore в Base64;
- `KEYSTORE_PASSWORD`;
- `KEY_ALIAS`.

Секреты не добавлены. Локальная сборка использует `signing.local.props`.

## Известные предупреждения

- `XA0141`: `libe_sqlcipher.so` и `libSkiaSharp.so` меньше 16 КБ и не соответствуют требованию 16 КБ страниц Android 16. Для текущего ТСД (Android 11) не критично; при переходе на Android 16 потребуется обновить пакеты.
- `targetSdk 36`: приложение проверено на Android 11; поведение на Android 16+ требует отдельной проверки.
