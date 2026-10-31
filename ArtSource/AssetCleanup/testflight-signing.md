# TestFlight signing and rebuild

- Apple Distribution certificate `GJ379DTCHM`, team `U3D67WUK3R`, created using the existing `D:\iOS\ios-signing.csr` with user approval.
- App Store profile `Knights Gauntlet Unity TestFlight Distribution` (`4QA7R88SJ2`) uses bundle ID `com.apy.knightsgauntlet` and the new certificate.
- Verified distribution entitlements: `get-task-allow=false`, `beta-reports-active=true`, no provisioned devices, and matching certificate fingerprint.
- Existing Unity iOS credential `66ae1886-f2a2-434a-aac8-e009e0dc2044` updated successfully (HTTP 200). Unity UI confirms Apple Distribution identity.
- Encrypted signing identity and its private password file are stored outside the project in `D:\iOS`; no secrets recorded here.
- iOS build number advanced to `8`, after confirming TestFlight already contains builds 1–7 for version 1.
- Published only `ProjectSettings/ProjectSettings.asset` to Unity Version Control changeset `2`.
- Started Unity cloud build `#4` from `/main`, changeset `2`, macOS Tahoe / Xcode 26.5.0 / Apple Silicon / Mac Standard, with incremental caching.
- Build details: https://cloud.unity.com/home/organizations/20066193294712/projects/b44c8054-3626-4261-8fe1-5b9ea09a6861/cloud-build/buildtargets/knights-gauntlet-ios/builds/4
- User approved API agreement/access and Developer-role upload key creation. Apple granted API access immediately.
- Created team key `6LB4SMGCVL`, named `Knights Gauntlet TestFlight` (Apple's name limit required shortening), role Developer. Issuer `7bee2278-d744-45e2-9679-45056aa8bc40`.
- User additionally approved temporary Downloads storage followed by moving the key to `D:\iOS` with user-only access. Browser download timed out and reset automation, but Apple now marks the one-time key as downloaded. No `.p8` file was found in Downloads, `D:\iOS`, or the checked Windows Temp location. User asked to check for a pending browser download/Save dialog. Do not create another key or revoke this one without confirmation.
- An attempted wider profile search was rejected by automatic approval review because it could expose unrelated credentials. Keep further searches restricted to approved download locations.
- Build #4 reached Building and confirmed source changeset 2. Upload helper `testflight_upload.py` prepared from official Apple schemas and syntax checked; no authenticated upload attempted yet.
- Build #3 remains development-signed and must not be uploaded as the release build.

- User confirmed no pending download was visible and approved revoking the lost key and creating a replacement for manual browser download.
- Revoked only `6LB4SMGCVL`. Created replacement `375YGKKZTW`, name `Knights Gauntlet TestFlight 2`, role Developer, same issuer. Download button verified available; automation has NOT downloaded this replacement. User should download in their regular browser and save `AuthKey_375YGKKZTW.p8` to `D:\iOS`.

- User saved replacement `AuthKey_375YGKKZTW.p8` in `D:\iOS`. Restricted NTFS access to the current Windows account and successfully authenticated the Knight's Gauntlet app record through Apple's API; bundle ID matches.

- Unity build #4 succeeded. Downloaded IPA `20066193294712-knights-gauntlet-knights-gauntlet-ios-4.ipa` to the normal Downloads folder.
- Verified bundle ID `com.apy.knightsgauntlet`, version `1`, build `8`, distribution profile, `get-task-allow=false`, `beta-reports-active=true`, and no provisioned devices.
- Uploaded all 32 binary chunks and committed upload `efa5a5f8-13bc-40a0-ba3b-fd053864c8d4`. Intermittent connection resets required retries within the same reservation; no duplicate build reservation was created.
- Apple API confirms `PROCESSING` with empty errors and warnings after upload at 2026-10-17 05:14:49 America/Los_Angeles.

Next: verify final Apple processing result. No public release or tester invitations authorized.
