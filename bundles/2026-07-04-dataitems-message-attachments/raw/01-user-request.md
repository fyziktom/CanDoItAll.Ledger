# User Request

- Normal BOs such as Image BOs lost attached DataItems after encrypted message/DataItem privacy changes.
- Non-message BO DataItems must remain public in BO data so the UI can load them.
- One DataItem must be selected as main for gallery thumbnails.
- Files that cannot be displayed by the browser should still offer download, and every file should offer download.
- Messaging needs visible sent/received/replied indicators.
- Decrypted message attachments must render by file type, especially images, instead of showing base64/text.
- Original attachment type/name metadata for messages should remain inside encrypted content because encrypted IPFS payloads are binary.
- Rebuild and restart Docker instances and capture screenshots proving gallery and message attachment visibility.
