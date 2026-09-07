(() => {
  const reasons = document.getElementById("reasons");
  const feedback = document.getElementById("feedback-link");
  if (reasons && feedback) {
    let selected = "";
    reasons.addEventListener("click", (event) => {
      const btn = event.target.closest(".reason");
      if (!btn) return;

      reasons.querySelectorAll(".reason").forEach((el) => {
        el.setAttribute("aria-pressed", "false");
      });
      btn.setAttribute("aria-pressed", "true");
      selected = btn.getAttribute("data-reason") || "";

      const title = encodeURIComponent(`Uninstall feedback: ${selected || "other"}`);
      const body = encodeURIComponent(
        `Reason: ${selected || "(not selected)"}\n\nWhat happened:\n\n(Optional) OS / version:\n`
      );
      feedback.href = `https://github.com/royal-programmer/KeyFXBoard/issues/new?title=${title}&body=${body}`;
    });
  }

  document.querySelectorAll(".media-frame[data-media]").forEach((frame) => {
    const src = frame.getAttribute("data-media");
    const label = frame.getAttribute("data-label") || "Key FX Board";
    const placeholder = frame.querySelector(".media-placeholder");
    if (!src || !placeholder) return;

    const test = new Image();
    test.onload = () => {
      const img = document.createElement("img");
      img.src = src;
      img.alt = label;
      frame.replaceChildren(img);
    };
    test.src = src;
  });
})();
