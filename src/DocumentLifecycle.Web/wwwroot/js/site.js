const applicationTheme = "light";
try {
  localStorage.setItem("lte-theme", applicationTheme);
} catch {
  // The explicit document theme still works when browser storage is unavailable.
}
document.documentElement.setAttribute("data-bs-theme", applicationTheme);
document.documentElement.style.colorScheme = applicationTheme;

document.addEventListener("DOMContentLoaded", () => {
  const emailInput = document.querySelector("#Email");
  const passwordInput = document.querySelector("#Password");

  document.querySelectorAll("[data-demo-email]").forEach((button) => {
    button.addEventListener("click", () => {
      if (!(emailInput instanceof HTMLInputElement) || !(passwordInput instanceof HTMLInputElement)) {
        return;
      }

      emailInput.value = button.dataset.demoEmail ?? "";
      passwordInput.value = button.dataset.demoPassword ?? "";
      emailInput.dispatchEvent(new Event("change", { bubbles: true }));
      passwordInput.dispatchEvent(new Event("change", { bubbles: true }));
      passwordInput.focus();
    });
  });

  document.querySelectorAll("form[data-disable-on-submit]").forEach((form) => {
    form.addEventListener("submit", () => {
      const submitButton = form.querySelector("button[type='submit']");
      if (!(submitButton instanceof HTMLButtonElement)) {
        return;
      }

      form.setAttribute("aria-busy", "true");
      submitButton.disabled = true;
      submitButton.setAttribute("aria-disabled", "true");
      submitButton.textContent = submitButton.dataset.submitLabel ?? "Working…";
    });
  });
});
