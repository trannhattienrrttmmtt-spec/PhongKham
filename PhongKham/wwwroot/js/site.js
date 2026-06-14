const normalizeText = (value) => (value || "").toString().toLowerCase();

function setupTableFilter({ searchId, filterId, rowSelector }) {
  const search = document.getElementById(searchId);
  const filter = document.getElementById(filterId);
  const rows = Array.from(document.querySelectorAll(rowSelector));
  if (!rows.length || (!search && !filter)) return;

  const apply = () => {
    const query = normalizeText(search?.value);
    const state = filter?.value || "all";
    rows.forEach((row) => {
      const matchesQuery = !query || normalizeText(row.dataset.search).includes(query);
      const matchesState = state === "all" || row.dataset.state === state;
      row.hidden = !(matchesQuery && matchesState);
    });
  };

  search?.addEventListener("input", apply);
  filter?.addEventListener("change", apply);
  apply();
}

function setupPrescriptionDetailModal() {
  const modalElement = document.getElementById("prescriptionDetailModal");
  const modal = modalElement && window.bootstrap ? new bootstrap.Modal(modalElement) : null;

  document.querySelectorAll("[data-prescription-detail]").forEach((button) => {
    button.addEventListener("click", () => {
      document.getElementById("detailPatient").textContent = button.dataset.patient || "Đơn thuốc";
      document.getElementById("detailCreated").textContent = button.dataset.created || "";
      document.getElementById("detailDoctor").textContent = button.dataset.doctor || "";
      document.getElementById("detailDiagnosis").textContent = button.dataset.diagnosis || "";
      document.getElementById("detailInstructions").textContent = button.dataset.instructions || "";
      document.getElementById("detailAmount").textContent = button.dataset.amount || "";
      const isPending = (button.dataset.state || "") === "pending";
      document.getElementById("detailRejectId").value = button.dataset.id || "";
      document.getElementById("detailDispenseId").value = button.dataset.id || "";
      document.querySelectorAll(".detail-pending-action").forEach((action) => {
        action.hidden = !isPending;
      });

      const template = document.getElementById(button.dataset.detailTemplate || "");
      const detailLines = document.getElementById("detailLines");
      if (detailLines) {
        detailLines.innerHTML = template ? template.innerHTML : '<div class="empty-detail">Chưa có chi tiết thuốc.</div>';
      }
      modal?.show();
    });
  });
}

function setupDispenseConfirmModal() {
  const modalElement = document.getElementById("dispenseConfirmModal");
  const modal = modalElement && window.bootstrap ? new bootstrap.Modal(modalElement) : null;
  if (!modal) return;

  document.querySelectorAll("[data-confirm-dispense]").forEach((button) => {
    button.addEventListener("click", () => {
      document.getElementById("confirmDispenseId").value = button.dataset.id || "";
      document.getElementById("confirmDispensePatient").textContent = button.dataset.patient || "Đơn thuốc";
      document.getElementById("confirmDispenseDoctor").textContent = button.dataset.doctor || "";
      document.getElementById("confirmDispenseDiagnosis").textContent = button.dataset.diagnosis || "";
      document.getElementById("confirmDispenseAmount").textContent = button.dataset.amount || "";

      const template = document.getElementById(button.dataset.detailTemplate || "");
      const lines = document.getElementById("confirmDispenseLines");
      if (lines) {
        lines.innerHTML = template ? template.innerHTML : '<div class="empty-detail">Chưa có chi tiết thuốc.</div>';
      }

      modal.show();
    });
  });
}

function setupFocusButtons() {
  document.querySelectorAll("[data-focus-target]").forEach((button) => {
    button.addEventListener("click", () => {
      document.getElementById(button.dataset.focusTarget)?.focus();
    });
  });
}

function setupMedicineEditModal() {
  const modalElement = document.getElementById("medicineEditModal");
  const modal = modalElement && window.bootstrap ? new bootstrap.Modal(modalElement) : null;
  if (!modal) return;

  document.querySelectorAll("[data-medicine-edit]").forEach((button) => {
    button.addEventListener("click", () => {
      document.getElementById("editMedicineId").value = button.dataset.id || "";
      document.getElementById("editMedicineCode").value = button.dataset.code || "";
      document.getElementById("editMedicineSmiles").value = button.dataset.smiles || "";
      document.getElementById("editMedicineName").value = button.dataset.name || "";
      document.getElementById("editMedicineUnit").value = button.dataset.unit || "";
      document.getElementById("editMedicineQuantity").value = button.dataset.quantity || "0";
      document.getElementById("editMedicineMinimum").value = button.dataset.minimum || "30";
      document.getElementById("editMedicinePrice").value = button.dataset.price || "0";
      document.getElementById("editMedicineExpiry").value = button.dataset.expiry || "";
      document.getElementById("editMedicineIsActive").value = button.dataset.active || "true";
      modal.show();
    });
  });
}

function setupClinicInfoModal() {
  const modalElement = document.getElementById("clinicInfoModal");
  const modal = modalElement && window.bootstrap ? new bootstrap.Modal(modalElement) : null;
  if (!modal) return;

  document.querySelectorAll("[data-clinic-info]").forEach((button) => {
    button.addEventListener("click", () => modal.show());
  });
}

function setupActiveNavigation() {
  const currentPath = window.location.pathname.toLowerCase();
  document.querySelectorAll(".side-nav a").forEach((link) => {
    const path = new URL(link.href, window.location.origin).pathname.toLowerCase();
    if (path === currentPath) {
      link.classList.add("active");
    }
  });
}

setupTableFilter({
  searchId: "medicineSearch",
  filterId: "medicineFilter",
  rowSelector: "[data-medicine-row]"
});

setupTableFilter({
  searchId: "prescriptionSearch",
  filterId: "prescriptionFilter",
  rowSelector: "[data-prescription-row]"
});

setupTableFilter({
  searchId: "receiptSearch",
  rowSelector: "[data-receipt-row]"
});

setupTableFilter({
  searchId: "transactionSearch",
  filterId: "transactionFilter",
  rowSelector: "[data-transaction-row]"
});

setupTableFilter({
  searchId: "lotSearch",
  filterId: "lotFilter",
  rowSelector: "[data-lot-row]"
});

setupTableFilter({
  searchId: "expirySearch",
  filterId: "expiryFilter",
  rowSelector: "[data-expiry-row]"
});

setupTableFilter({
  searchId: "auditSearch",
  rowSelector: "[data-audit-row]"
});

setupPrescriptionDetailModal();
setupDispenseConfirmModal();
setupFocusButtons();
setupMedicineEditModal();
setupClinicInfoModal();
setupActiveNavigation();
