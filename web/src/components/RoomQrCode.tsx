import { useEffect, useState } from "react";
import QRCode from "qrcode";

/**
 * W-13 · The room's check-in QR as a printable sticker. The code is drawn in the browser (the
 * `qrcode` package), so nothing leaves the page; Print uses the print stylesheet in index.css,
 * which hides everything except this block (the code and the room name).
 */
export function RoomQrCode({ roomName, code }: { roomName: string; code: string }) {
  const [dataUrl, setDataUrl] = useState<string | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let active = true;
    QRCode.toDataURL(code, { margin: 1, width: 360, errorCorrectionLevel: "M" })
      .then((url) => active && setDataUrl(url))
      .catch(() => active && setFailed(true));
    return () => {
      active = false;
    };
  }, [code]);

  if (failed) return <p className="form-error">The QR code could not be drawn.</p>;

  return (
    <div className="qr-print" style={{ marginTop: 12, display: "grid", justifyItems: "start", gap: 8 }}>
      {dataUrl ? (
        <img src={dataUrl} alt={`Check-in QR code for ${roomName}`} width={180} height={180} />
      ) : (
        <div className="state-view">Drawing the QR code…</div>
      )}
      <h2 style={{ display: "none" }} className="print-only">{roomName}</h2>
      <button type="button" className="btn btn-secondary no-print" onClick={() => window.print()} disabled={!dataUrl}>
        Print QR sticker
      </button>
    </div>
  );
}
