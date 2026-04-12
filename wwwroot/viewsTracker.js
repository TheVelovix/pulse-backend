(function () {
  var projectId = document.currentScript.getAttribute("data-project-id");
  if (!projectId) return;

  var data = {
    projectId: projectId,
    url: window.location.href,
    referrer: document.referrer || null,
  };

  fetch("https://api.pulse.velovix.com/api/track", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(data),
  });
})();
