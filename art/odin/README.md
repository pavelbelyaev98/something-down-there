# Odin editor tooling
- Item: Odin Inspector and Serializer + Odin Validator, matching **4.0.2.4**.
- Purpose: content authoring/tuning and asset validation before builds.
- Source/License: user-purchased Sirenix packages from Unity Asset Store; use under the purchased seat license, private repository only. [Publisher](https://odininspector.com/).
- Unity Path: `Assets/Plugins/Sirenix`; project profile: `Assets/Editor/Validation/MainGameValidation.asset`.
- Status: installed; Editor Only mode enabled, MainGame/content build validation enabled; runtime serializer unused.
- Maintenance: update both products together; retain `.meta` GUIDs, Editor Only configuration and Validator automation/profile settings. Vendor binaries use Git LFS; personal activation credentials are not repository content.
- Unity 6.6 import adjustment: reserialized the five outdated plugin importer metadata files after enabling Editor Only mode; GUIDs and publisher binaries preserved.
