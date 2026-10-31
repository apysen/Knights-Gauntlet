# Unity iOS cloud build #3

Build submitted successfully and verified as **Sent to Builder**.

- Target: Knights Gauntlet iOS
- Source: existing Unity Version Control repository `Knight-s Gauntlet/Knights Gauntlet`, branch `/main`, changeset `1`
- Published source: 775 reviewed project files; ignored SDK debug-symbol metadata was excluded. Local service credentials and Git metadata were excluded.
- Unity: 6000.5.0f1, Apple Silicon
- Builder: Mac Standard, macOS Tahoe, Xcode 26.5.0
- Profile: `Assets/Settings/Build Profiles/iOS.asset`
- Clean build: enabled
- Signing: existing configured credentials retained

[Build details](https://cloud.unity.com/home/organizations/20066193294712/projects/b44c8054-3626-4261-8fe1-5b9ea09a6861/cloud-build/buildtargets/knights-gauntlet-ios/builds/3)

The target's repository selection and branch were corrected to use the existing Unity Version Control repository. Earlier builds #1 and #2 failed during Git checkout; the current source provider is Unity Version Control. No Git commit or GitHub push was needed or performed; those local changes remain pending in Git.

The launch confirmation is saved as `cloud-build-3.jpg`. Build #3 subsequently succeeded. Its IPA is 50.39 MB.

## TestFlight signing check

The downloaded IPA's embedded provisioning profile has `get-task-allow = true`, one provisioned device, and no `beta-reports-active` entitlement. Unity's only saved iOS credential set identifies its certificate as `iPhone Developer`, although the set and profile names contain “App Store”. This is development signing and must be replaced with App Store distribution signing for TestFlight. No TestFlight upload has been performed.

Apple Developer sign-in is required to inspect or prepare the correct distribution certificate and provisioning profile. The Apple Developer sign-in tab has been opened for the user.
