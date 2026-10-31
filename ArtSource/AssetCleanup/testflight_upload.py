"""Upload an App Store-signed IPA with Apple's Build Uploads API.

Private key stays outside this repository. No JWTs or signed URLs are logged.
Docs: https://developer.apple.com/documentation/appstoreconnectapi/build-uploads
"""
import argparse
import base64
import hashlib
import json
import plistlib
from pathlib import Path
import time
import urllib.error
import urllib.request
import zipfile

from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec, utils

BASE = "https://api.appstoreconnect.apple.com/v1"


def b64(data):
    return base64.urlsafe_b64encode(data).rstrip(b"=").decode()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--key", required=True, type=Path)
    parser.add_argument("--key-id", required=True)
    parser.add_argument("--issuer", required=True)
    parser.add_argument("--app-id", default="6790239798")
    parser.add_argument("--expected-build", default="9")
    parser.add_argument("--ipa", type=Path)
    parser.add_argument("--status", help="Read an existing build upload ID")
    parser.add_argument("--resume", help="Retry an existing build upload reservation")
    parser.add_argument("--inspect-icons", help="Inspect icons from an earlier build")
    parser.add_argument("--download-icon", help="Save an earlier build's 1024px icon")
    parser.add_argument("--icon-output", type=Path)
    args = parser.parse_args()
    key = serialization.load_pem_private_key(args.key.read_bytes(), password=None)

    def api(method, path, payload=None):
        now = int(time.time())
        header = b64(json.dumps({"alg": "ES256", "kid": args.key_id, "typ": "JWT"}).encode())
        claims = b64(json.dumps({"iss": args.issuer, "iat": now - 10,
                                 "exp": now + 600, "aud": "appstoreconnect-v1"}).encode())
        signing = (header + "." + claims).encode()
        r, s = utils.decode_dss_signature(key.sign(signing, ec.ECDSA(hashes.SHA256())))
        token = signing.decode() + "." + b64(r.to_bytes(32, "big") + s.to_bytes(32, "big"))
        data = None if payload is None else json.dumps({"data": payload}).encode()
        req = urllib.request.Request(BASE + path, data=data, method=method,
                                     headers={"Authorization": "Bearer " + token,
                                              "Content-Type": "application/json"})
        try:
            with urllib.request.urlopen(req, timeout=120) as response:
                return json.load(response)
        except urllib.error.HTTPError as error:
            details = json.loads(error.read()).get("errors", [])
            raise RuntimeError(json.dumps({"http": error.code, "errors": details})) from None

    app = api("GET", "/apps/" + args.app_id)["data"]
    if app["attributes"]["bundleId"] != "com.apy.knightsgauntlet":
        raise RuntimeError("App record bundle ID does not match Knight's Gauntlet")
    print("Authenticated app:", app["attributes"]["name"], flush=True)
    if args.inspect_icons or args.download_icon:
        build_id = args.inspect_icons or args.download_icon
        icons = api("GET", "/builds/" + build_id + "/icons")["data"]
        for icon in icons:
            attrs = icon["attributes"]
            asset = attrs.get("iconAsset") or {}
            if args.download_icon:
                if attrs.get("masked") is not False:
                    continue
                if asset.get("width") != 1024 or asset.get("height") != 1024 or not asset.get("templateUrl"):
                    continue
                import re
                template = asset["templateUrl"]
                parts = urllib.parse.urlsplit(template)
                print("Icon asset host:", parts.hostname, "template fields:", re.findall(r"\{([^}]+)\}", template), flush=True)
                if not args.icon_output:
                    raise RuntimeError("--icon-output is required")
                url = template.replace("{w}", "1024").replace("{h}", "1024").replace("{f}", "png")
                request = urllib.request.Request(url, headers={"Accept": "image/png"})
                with urllib.request.urlopen(request, timeout=60) as response:
                    image_bytes = response.read()
                    if not response.headers.get("Content-Type", "").startswith("image/"):
                        raise RuntimeError("Apple icon endpoint did not return an image")
                args.icon_output.parent.mkdir(parents=True, exist_ok=True)
                args.icon_output.write_bytes(image_bytes)
                print("Saved 1024px icon bytes:", len(image_bytes), flush=True)
                return
            print(json.dumps({"id": icon["id"], "name": attrs.get("name"),
                              "iconType": attrs.get("iconType"), "masked": attrs.get("masked"),
                              "assetFields": list(asset), "width": asset.get("width"), "height": asset.get("height")}))
        return
    if args.status:
        result = api("GET", "/buildUploads/" + args.status)["data"]
        print(json.dumps({"uploadId": result["id"], **result["attributes"]}), flush=True)
        return
    if not args.ipa:
        return
    with zipfile.ZipFile(args.ipa) as archive:
        info_name = next(n for n in archive.namelist()
                         if n.startswith("Payload/") and n.endswith(".app/Info.plist")
                         and n.count("/") == 2)
        info = plistlib.loads(archive.read(info_name))
        profile_data = archive.read(info_name.removesuffix("Info.plist") + "embedded.mobileprovision")
        start = profile_data.index(b"<?xml")
        end = profile_data.index(b"</plist>", start) + len(b"</plist>")
        profile = plistlib.loads(profile_data[start:end])
    entitlements = profile["Entitlements"]
    assert info["CFBundleIdentifier"] == "com.apy.knightsgauntlet"
    assert info["CFBundleVersion"] == args.expected_build, "Unexpected iOS build number"
    assert entitlements.get("get-task-allow") is False, "IPA is development-signed"
    assert entitlements.get("beta-reports-active") is True
    assert not profile.get("ProvisionedDevices"), "IPA uses device-limited signing"
    if args.resume:
        upload = {"id": args.resume}
    else:
        upload = api("POST", "/buildUploads", {
        "type": "buildUploads",
        "attributes": {"cfBundleShortVersionString": info["CFBundleShortVersionString"],
                       "cfBundleVersion": info["CFBundleVersion"], "platform": "IOS"},
        "relationships": {"app": {"data": {"type": "apps", "id": args.app_id}}}})["data"]
    print("Build upload ID:", upload["id"], flush=True)
    if args.resume:
        files = api("GET", "/buildUploads/" + upload["id"] + "/buildUploadFiles")["data"]
        reservation = next(f for f in files if f["attributes"]["assetType"] == "ASSET")
        reservation = api("GET", "/buildUploadFiles/" + reservation["id"])["data"]
    else:
        reservation = api("POST", "/buildUploadFiles", {
        "type": "buildUploadFiles",
        "attributes": {"assetType": "ASSET", "fileName": args.ipa.name,
                       "fileSize": args.ipa.stat().st_size, "uti": "com.apple.ipa"},
        "relationships": {"buildUpload": {"data": {"type": "buildUploads", "id": upload["id"]}}}})["data"]
    digest = hashlib.md5()
    with args.ipa.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
        operations = reservation["attributes"]["uploadOperations"]
        for index, operation in enumerate(operations, 1):
            stream.seek(operation["offset"])
            block = stream.read(operation["length"])
            assert len(block) == operation["length"]
            headers = {h["name"]: h["value"] for h in operation.get("requestHeaders", [])}
            req = urllib.request.Request(operation["url"], data=block,
                                         method=operation["method"], headers=headers)
            for attempt in range(3):
                try:
                    with urllib.request.urlopen(req, timeout=180) as response:
                        response.read()
                    break
                except urllib.error.HTTPError as error:
                    print("Chunk", index, "HTTP", error.code, flush=True)
                    if attempt == 2:
                        raise RuntimeError("Chunk upload failed; signed URL omitted from logs") from None
                    time.sleep(2)
                except (urllib.error.URLError, ConnectionError, TimeoutError) as error:
                    print("Chunk", index, "network error", type(getattr(error, "reason", error)).__name__, flush=True)
                    if attempt == 2:
                        raise RuntimeError("Chunk upload failed; signed URL omitted from logs") from None
                    time.sleep(2)
            print("Uploaded chunk", index, "of", len(operations), flush=True)
    api("PATCH", "/buildUploadFiles/" + reservation["id"], {
        "type": "buildUploadFiles", "id": reservation["id"],
        "attributes": {"uploaded": True,
                       "sourceFileChecksums": {"file": {"algorithm": "MD5", "hash": digest.hexdigest()}}}})
    print("Upload committed. Check processing with --status", upload["id"], flush=True)


if __name__ == "__main__":
    main()
