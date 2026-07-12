// Supabase Edge Function: emails a bug report to the developer whenever a row is
// inserted into public.bug_reports. Wired up by a Database Webhook (Database ->
// Webhooks) that fires on INSERT and POSTs the new row here.
//
// Secrets it needs (Project Settings -> Edge Functions -> Manage secrets, or
// `supabase secrets set`):
//   RESEND_API_KEY   an API key from https://resend.com (free tier is fine)
//   BUG_TO_EMAIL     where to send reports        (default apysen02@gmail.com)
//   BUG_FROM_EMAIL   verified sender in Resend    (default onboarding@resend.dev)
//   WEBHOOK_SECRET   optional; if set, the webhook must send a matching
//                    "x-webhook-secret" header or the request is rejected.

interface BugRecord {
  id?: number;
  message?: string;
  name?: string;
  app_version?: string;
  device?: string;
  os?: string;
  created_at?: string;
}

Deno.serve(async (req) => {
  // Optional shared-secret check so only your webhook can trigger emails.
  const expected = Deno.env.get("WEBHOOK_SECRET");
  if (expected && req.headers.get("x-webhook-secret") !== expected) {
    return new Response("unauthorized", { status: 401 });
  }

  const apiKey = Deno.env.get("RESEND_API_KEY");
  if (!apiKey) return new Response("RESEND_API_KEY not set", { status: 500 });

  const to = Deno.env.get("BUG_TO_EMAIL") ?? "apysen02@gmail.com";
  const from = Deno.env.get("BUG_FROM_EMAIL") ?? "onboarding@resend.dev";

  let record: BugRecord = {};
  try {
    const payload = await req.json();
    record = payload.record ?? payload; // webhook wraps the row in `record`
  } catch {
    return new Response("bad payload", { status: 400 });
  }

  const esc = (s: string) =>
    s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
  const line = (label: string, value?: string) =>
    value ? `<tr><td style="padding:2px 10px 2px 0;color:#888">${label}</td><td>${esc(value)}</td></tr>` : "";

  const html = `
    <h2>New Knight's Gauntlet bug report</h2>
    <p style="white-space:pre-wrap;font-size:15px">${esc(record.message ?? "(empty)")}</p>
    <table style="font-size:13px;border-collapse:collapse;margin-top:12px">
      ${line("From", record.name)}
      ${line("App version", record.app_version)}
      ${line("Device", record.device)}
      ${line("OS", record.os)}
      ${line("Received", record.created_at)}
      ${line("Report ID", record.id != null ? String(record.id) : undefined)}
    </table>`;

  const res = await fetch("https://api.resend.com/emails", {
    method: "POST",
    headers: { "Authorization": `Bearer ${apiKey}`, "Content-Type": "application/json" },
    body: JSON.stringify({
      from: `Knight's Gauntlet <${from}>`,
      to: [to],
      subject: "New bug report" + (record.name ? ` from ${record.name}` : ""),
      html,
    }),
  });

  if (!res.ok) {
    const err = await res.text();
    console.error("Resend failed:", res.status, err);
    return new Response("email failed: " + err, { status: 502 });
  }
  return new Response("ok", { status: 200 });
});
