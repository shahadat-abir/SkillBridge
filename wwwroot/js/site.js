// SkillBridge client helpers (progressive enhancement only - all rules are enforced on the server)
document.addEventListener("DOMContentLoaded", function () {
    // Confirmation for important / destructive actions: <form data-confirm="...">
    document.querySelectorAll("form[data-confirm]").forEach(function (form) {
        form.addEventListener("submit", function (e) {
            if (!window.confirm(form.getAttribute("data-confirm"))) e.preventDefault();
        });
    });

    // Job form: show participant count only for challenge mode
    var mode = document.getElementById("Mode");
    var participants = document.getElementById("participants-group");
    if (mode && participants) {
        var sync = function () { var isChallenge = mode.value === "2" || mode.value === "Challenge"; participants.classList.toggle("d-none", !isChallenge); };
        mode.addEventListener("change", sync);
        sync();
    }
});
