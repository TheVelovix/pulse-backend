(function () {
  var projectId = document.currentScript.getAttribute("data-project-id");
  if (!projectId) return;
  var storageKey = "pulse_last_tracked_" + projectId + "_" + window.location.pathname;
  var lastTracked = localStorage.getItem(storageKey);
  var now = Date.now();
  if (!lastTracked || now - parseInt(lastTracked) >= 86400000) {
    localStorage.setItem(storageKey, now.toString());
    fetch("https://api.pulse.velovix.com/api/track", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        projectId: projectId,
        url: window.location.href,
        referrer: document.referrer || null,
      }),
    });
  }
  function sendHeartbeat() {
    let visitorId = localStorage.getItem("pulse_visitor_id");
    if (!visitorId) {
      visitorId = crypto.randomUUID();
      localStorage.setItem("pulse_visitor_id", visitorId);
    }
    fetch("https://api.pulse.velovix.com/api/heartbeat", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ projectId, visitorId }),
    });
  }
  sendHeartbeat();
  setInterval(sendHeartbeat, 30000);
})();
