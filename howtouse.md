# How to Use RR to RPM2

RR to RPM2 downloads RadioReference P25 trunked system data and exports CSV files for Harris RPM2.

## What You Need

- Windows
- RadioReference Premium account
- Approved RadioReference API app key
- The RadioReference username, not an email address

The app key box accepts either the plain app key or a value formatted like:

```text
base64:YOUR_ENCODED_KEY
```

## Start and Login

1. Open the app.
2. Enter your RadioReference username.
3. Enter your RadioReference password.
4. Enter your API app key.
5. Optional: check `Save credentials`.
6. Click `Login`.

If saved credentials are available, the app auto-connects to RadioReference when it launches. The bottom status bar and progress bar show what the app is doing.

Use `Forget` to remove saved credentials.

## Find a Trunked System

You can load a system by RadioReference SID, SysID lookup, or state/county search.

### Load by SID

1. Enter the RadioReference system SID in `RR system SID`.
2. Click `Load TGIDs`.

### Lookup by SysID

1. Enter the system ID in `Lookup by SysID`.
2. Click `Lookup`.
3. Pick a system from `Lookup results`.
4. Click `Load TGIDs`.

### Search by State or County

1. Pick a `Search state`.
2. Optional: pick a `Search county`.
3. Click `Search County`.
4. Pick a system from `Lookup results`.
5. Click `Load TGIDs`.

## Progress Bar

During `Load TGIDs`, the progress bar steps through:

1. Loading system details
2. Downloading talkgroups
3. Downloading sites
4. Populating talkgroups
5. Populating sites
6. Refreshing filters

Other RadioReference actions show a moving busy bar because exact progress is not available from the API.

## TGIDs Tab

Use the `TGIDs` tab to review and export talkgroups.

### Filter Talkgroups

- `Filter text`: searches TGID, alpha, description, category, and tag.
- `Counties/categories`: check or clear categories.
- `Tags`: check or clear tags.
- `Include encrypted`: include or hide encrypted talkgroups.

### Select Talkgroups

- Each row has an `Export` checkbox.
- `Select visible TGIDs` checks all currently visible filtered rows.
- `Clear visible TGIDs` clears all currently visible filtered rows.

Only checked visible talkgroups are exported.

### Talkgroup Export Settings

Before exporting, review:

- `RPM2 group set name`
- `RPM2 system ID`
- `Visual ID color`
- `Long name source`
- Talkgroup option checkboxes such as transmit, receive, scan, backlight, and scan list member

Click `Export RPM2 CSV` to export selected talkgroups.

## Sites Tab

Use the `Sites` tab to select sites and export site-related CSV files.

Sites are checked by default after loading a system.

### Filter Sites by County

The `Site counties` list filters the visible site list.

- `All`: shows all site counties.
- `Clear`: clears all site county filters.

The site county filter changes what is visible. It does not automatically select or unselect hidden sites.

### Select Sites

- Check or uncheck individual sites in `Sites/control channels`.
- `Select visible`: checks all sites currently visible after filtering.
- `Clear all`: unchecks every loaded site.

Both site exports use the same checked site list.

## Export Site Frequency CSV

Use this when RPM2 needs a trunked frequency set/control channel CSV.

1. Go to the `Sites` tab.
2. Check the sites you want.
3. Set `TX fallback`.
4. Check the frequency types to include:
   - `Primary`: RadioReference use code `d`
   - `Alternate`: RadioReference use code `a`
   - `Control`: RadioReference use code `c`
   - `Other`: frequencies without a primary, alternate, or control marker
5. Click `Export Site CSV`.

The exporter uses selected site frequencies that match the checked frequency types. `TX fallback` accepts VHF, UHF, 700 MHz, and 800 MHz ranges.

## Export Site Alias CSV

Use this when RPM2 needs site aliases.

1. Go to the `Sites` tab.
2. Check the sites you want.
3. Set `Alias list name`.
4. Set `P25 WAN list`.
5. Set `WA network`.
6. Click `Export Alias CSV`.

The export format is:

```csv
Name,Type,P25 WAN List,Site Name,WA Network,SITE ID,RFSS ID
STEL0001,Site Alias,P25LS001,,,,
STEL0001,,,MIDDLEPO,MARCS,0x1,0x0
STEL0001,,,ANOTHERS,MARCS,0x2,0x0
```

Rules:

- Line 1 is the CSV header.
- Line 2 defines the site alias list.
- Lines 3 and later are selected sites.
- `Name` stays the same all the way down.
- `Site Name` is generated from the RadioReference site description, capped at 8 alphanumeric characters.
- `SITE ID` and `RFSS ID` are populated per site when available.

## Tips

- If the site list is long, scroll inside the site list box.
- Use `Clear all` on the Sites tab before building a smaller site export set.
- Use county filters first, then `Select visible`, to quickly select sites from one county.
- If login fails, verify your RadioReference username, password, Premium status, and API app key.
