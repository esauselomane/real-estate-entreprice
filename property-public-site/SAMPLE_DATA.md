# Local end-to-end sample scenario

The sample data belongs to **TEN-001 (Agency 1)**, verified against the local SuperAdmin account. It is stored in the local SQL Server databases, not mock browser responses. Existing records are preserved.

- Public website: http://localhost:3010
- Agency portal: http://localhost:5173 (use your existing SuperAdmin sign-in)
- Owner email: `owner.public-e2e@example.test`
- Owner password: `BiscoveSampleOwner2026!`

These credentials belong exclusively to the fictional local sample owner. The generated agency API key is stored in the ignored `property-public-site/.env` and is not printed. Sample contact numbers and schematic images are fictional.

## Fixtures

| Sample marker | Area | Type | Purpose | Initial state |
| --- | --- | --- | --- | --- |
| SEA-POINT | Sea Point, Cape Town | Residential | Sale | Public, hot |
| GREEN-POINT | Green Point, Cape Town | Residential | Rent | Public, hot |
| CENTURY-CITY | Century City, Cape Town | Commercial | Sale and rent | Public, hot |
| WOODSTOCK | Woodstock, Cape Town | Industrial | Rent | Public |
| GARDENS | Gardens, Cape Town | Retail | Sale and rent | Public |
| CONSTANTIA | Constantia, Cape Town | Land | Sale | Public |
| DISTANT | Hatfield, Pretoria | Residential | Sale | Public; outside Cape Town nearby radius |
| PRIVATE-ROOM | Observatory, Cape Town | Room share/let | Private room to let · R5,500/month | Public |
| ROOM-SHARE | Observatory, Cape Town | Room share/let | Shared room · R3,200/month | Public |
| HIDDEN | Sea Point, Cape Town | Residential | Sale | Unpublished |
| REVIEW | Green Point, Cape Town | Residential | Rent | Pending review |

Every fixture has three schematic PNG gallery images, two units with images, prices, coordinates and the sample owner's contacts. One unit is vacant and one occupied. Addresses begin with `SAMPLE E2E`; descriptions contain `[PUBLIC-SITE-E2E:<marker>]` so fixtures can be identified safely.

## Try the workflow

1. Open the public website: 48 approved sample properties are available, with rooms shown by default and up to five randomly selected hot rooms shown on the landing page.
2. Search **Sea Point** with nearby areas enabled and radius **15 km**. Cape Town samples are returned; Pretoria is excluded. Disable nearby areas to see only the approved Sea Point match.
3. Filter Residential, Commercial, Retail, Industrial or Land; test Sale/Rent and price ranges. Open a property to inspect its images, units, location and owner contacts.
4. In **My properties**, sign in with the sample owner. All 50 owner properties are listed with their statuses, including unpublished and pending records.
5. In the agency portal, open Property Search and select **Owner submissions awaiting review**. Open the Green Point `REVIEW` fixture. In **Public website**, approve publication and optionally mark it hot using an administrator account belonging to that agency, then save.
6. Confirm it appears publicly. Uncheck visibility and save; public detail and search must exclude it.
7. Edit that property through the owner's workspace. Saving returns it to **Pending review**, unpublished, preserving any administrator-set hot designation. The API verification script finishes with this fixture pending so you can repeat the exercise.
8. Use **Sell or rent through us** to create another property as the sample owner, then review it in the agency portal.

## Repeat setup and checks

From the repository root, with local SQL Server running:

```sh
dotnet run --project tools/PublicSite.SampleData -- "$PWD"
python3 scripts/qa/start-public-site-testing.py
python3 scripts/qa/verify-public-site-sample-data.py
```

The seed utility reads Developer.API local connection settings and rejects non-loopback SQL hosts. It verifies SuperAdmin's agency from Identity, adds missing fixture markers without duplicating or resetting existing properties, preserves existing sample owner edits/passwords, and reuses a valid configured agency key. It creates a new local key only if the configured key is missing, expired, revoked or for another agency. An Identity database with the seeded SuperAdmin must already exist.

The launcher starts only missing local services. It disables email delivery and leaves existing listeners unchanged. Logs are `/tmp/public-e2e-<service>.log`. The verifier changes only the labelled `REVIEW` fixture, exercises publication/withdrawal/owner resubmission, and leaves it pending. Supply `QA_ADMIN_EMAIL` / `QA_PASSWORD` through the environment if your local SuperAdmin credentials differ from the repository defaults.

This setup does not deploy or create records in a live hosted environment.

The live setup also exposed and corrected legacy startup backfills that were reassigning newer agency properties and overwriting their sale/rent flags and coordinates. Those backfills now apply only to their original legacy demo rows and exclude public owner submissions.

Search now includes **For sale**, **To let**, **Sale & to let** (requires both flags), **All listings**. **Room share/let** is a property type, independent of sale or letting, and can be filtered to private or shared rooms. Owners choose the room offering when submitting, and agencies can review/change it in Public website settings.

The seeder keeps the original 11 workflow fixtures and adds 39 randomized properties (areas, types, prices, coordinates and hot selections), for **50 total**. Reruns preserve existing records and edits. The public website needs no visitor sign-in for browsing, searching or owner contacts. Owner accounts are required only for submitting or managing listings. Cards use the API `thumb` image; details keep the full gallery.
