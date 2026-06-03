# RadioReference to RPM2

Windows WPF utility for downloading RadioReference trunked-system talkgroups and exporting a Harris RPM2 talkgroup CSV.

## Requirements

- Windows
- .NET 8 Desktop Runtime or SDK
- RadioReference account with active Premium subscription
- Approved RadioReference API app key

RadioReference's database API is SOAP/XML. The app asks for the end user's RadioReference username and password, then uses the configured API app key on API calls. The app key field accepts either a plain app key or a `base64:` encoded app key.

Saved credentials are encrypted with Windows user-scope DPAPI and stored at `%LOCALAPPDATA%\RR-RPM2\credentials.dat`. They can only be decrypted by the same Windows user account that saved them.

## RadioReference API Key

1. Sign in to RadioReference with an account that has an active Premium subscription.
2. Open the API application form: https://www.radioreference.com/account/api/apply
3. Request an app key for this application.
4. After RadioReference approves the request, enter the app key in the app's `API app key` field.

Use the RadioReference username, not an email address, when logging in through this app.

### Suggested API Application Answers

**Application Name**

RR to RPM2

**Short Description**

A private Windows utility that converts authorized RadioReference trunked system data into Harris RPM2 import CSV files.

**Detailed Use Case**

RR to RPM2 is a private/internal Windows desktop application used to help program Harris radios in RPM2. The application allows an authorized RadioReference Premium user to enter their RadioReference username, password, API app key, and a trunked system SID or SysID. It then retrieves trunked system information from the RadioReference Database Web Service API and converts selected data into Harris RPM2-compatible CSV import files.

The application is intended for personal/internal radio programming use by the account holder or authorized radio programming staff. It is not intended to redistribute RadioReference data, publish database content, or provide public access to the API.

The application intends to call RadioReference SOAP API operations including `getUserData` to verify login, `getTrsBySysid` to look up systems by SysID, `getTrsDetails` to retrieve system details, `getTrsTalkgroupCats` and `getTrsTalkgroups` to retrieve and categorize talkgroups, and `getTrsSites` to retrieve trunked system site and control-channel information.

API data is displayed inside the application so the user can review talkgroups, filter by category/county, exclude encrypted talkgroups, choose RPM2 export options, select nearby sites/control channels, and export CSV files for import into Harris RPM2. Talkgroup data is used to generate RPM2 P25 Group Set rows, and site control-channel data is used to generate RPM2 Trunked Frequency Set rows. RadioReference data is used only during this conversion workflow and is not sold, republished, or made available to the general public.

**Distribution**

Private / Internal

**Platform**

Desktop / Windows

## Run

```powershell
cd .\RrRpm2
dotnet run
```

## Release Builds

GitHub Actions publishes a Windows x64 build when a tag starting with `v` is pushed:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

The release asset is `RR-to-RPM2-win-x64.zip`.

## Workflow

1. Enter RadioReference username, password, and API app key.
2. Optionally check `Save credentials`.
3. Click `Login`.
4. Enter a RadioReference trunked-system `sid`, or look one up by system SysID, state, or county.
5. Click `Load TGIDs`.
6. Filter by county/category or exclude encrypted talkgroups if desired.
7. Select nearby sites/control channels if exporting a site frequency CSV.
8. Click `Export RPM2 CSV` for talkgroups or `Export Site CSV` for control channels.

## RPM2 CSV Mapping

The exporter follows `RPM2 EXAMPLE.csv`:

- First row after the header is a `P25 Group Set`.
- Talkgroup rows use RR alpha tag for RPM2 `Group Name`, capped at 8 characters.
- Talkgroup rows use the selected long-name strategy, capped at 16 characters.
- Channel indexes are generated as `0001`, `0002`, etc.
- Default row flags match the sample export, including receive/transmit/calls/scan/backlight defaults.
- Site frequency exports follow `MARCS_VW_TRUNKED FQ.csv` and include selected RadioReference control/alternate-control frequencies.
