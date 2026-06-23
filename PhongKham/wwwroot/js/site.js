document.addEventListener("DOMContentLoaded", () => {
  document.querySelectorAll("form[data-confirm]").forEach(form => {
    form.addEventListener("submit", event => {
      if (!window.confirm(form.dataset.confirm)) event.preventDefault();
    });
  });

  const specialty = document.querySelector("#specialtySelect");
  const doctors = document.querySelector("#doctorSelect");
  if (specialty && doctors) {
    const normalizeSpecialty = value => (value || "")
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .replace(/\u0111/g, "d")
      .replace(/\u0110/g, "D")
      .trim()
      .toLocaleLowerCase("vi");

    const filterDoctors = () => {
      const selected = normalizeSpecialty(specialty.value);
      const options = [...doctors.options];

      options.forEach(option => {
        const matches = !selected || normalizeSpecialty(option.dataset.specialty) === selected;
        option.hidden = !matches;
        option.disabled = !matches;
      });

      const selectedDoctor = doctors.selectedOptions[0];
      if (!selectedDoctor || selectedDoctor.disabled) {
        const firstVisible = options.find(option => !option.disabled);
        if (firstVisible) {
          doctors.value = firstVisible.value;
        }
      }
    };
    specialty.addEventListener("change", filterDoctors);
    filterDoctors();

    document.querySelectorAll("[data-suggest-doctor]").forEach(button => {
      button.addEventListener("click", () => {
        const matchedSpecialty = [...specialty.options].find(option =>
          normalizeSpecialty(option.value) === normalizeSpecialty(button.dataset.suggestSpecialty));
        if (matchedSpecialty) {
          specialty.value = matchedSpecialty.value;
          filterDoctors();
        }

        if (button.dataset.suggestDoctor) {
          doctors.value = button.dataset.suggestDoctor;
          doctors.dispatchEvent(new Event("change", { bubbles: true }));
        }

        const dateInput = document.querySelector("#appointmentDateInput");
        const timeSelect = document.querySelector("#appointmentTimeSelect");
        if (dateInput && button.dataset.suggestDate) {
          dateInput.value = button.dataset.suggestDate;
        }
        if (timeSelect && button.dataset.suggestTime) {
          const matchedTime = [...timeSelect.options].find(option =>
            option.value === button.dataset.suggestTime || option.textContent.trim() === button.dataset.suggestTime);
          if (matchedTime) {
            timeSelect.value = matchedTime.value;
          }
        }

        document.querySelectorAll("[data-suggest-doctor]").forEach(item => item.classList.remove("active"));
        button.classList.add("active");
      });
    });
  }

  const appointmentFilter = document.querySelector("[data-appointment-filter]");
  if (appointmentFilter) {
    const rows = [...document.querySelectorAll("[data-appointment-row]")];
    const search = appointmentFilter.querySelector("[data-appointment-search]");
    let status = "all";
    const apply = () => {
      const query = search.value.trim().toLocaleLowerCase("vi");
      const today = new Date().toISOString().slice(0, 10);
      rows.forEach(row => {
        const matchesText = !query || row.textContent.toLocaleLowerCase("vi").includes(query);
        const matchesStatus = status === "all"
          || (status === "upcoming" && row.dataset.date >= today && row.dataset.status !== "Đã hủy")
          || row.dataset.status === status;
        row.hidden = !(matchesText && matchesStatus);
      });
    };
    appointmentFilter.querySelectorAll("[data-status]").forEach(button => {
      button.addEventListener("click", () => {
        appointmentFilter.querySelectorAll("[data-status]").forEach(item => item.classList.remove("active"));
        button.classList.add("active");
        status = button.dataset.status;
        apply();
      });
    });
    search.addEventListener("input", apply);
  }

  const historyFilter = document.querySelector("[data-history-filter]");
  if (historyFilter) {
    const apply = () => {
      const from = historyFilter.querySelector("[data-history-from]").value;
      const to = historyFilter.querySelector("[data-history-to]").value;
      const query = historyFilter.querySelector("[data-history-search]").value.trim().toLocaleLowerCase("vi");
      document.querySelectorAll("[data-history-row]").forEach(row => {
        const date = row.dataset.date;
        row.hidden = Boolean((from && date < from) || (to && date > to)
          || (query && !row.textContent.toLocaleLowerCase("vi").includes(query)));
      });
    };
    historyFilter.querySelector("[data-history-submit]").addEventListener("click", apply);
    historyFilter.querySelector("[data-history-search]").addEventListener("input", apply);
  }

  const notificationTabs = document.querySelector("[data-notification-tabs]");
  if (notificationTabs) {
    notificationTabs.querySelectorAll("[data-category]").forEach(button => {
      button.addEventListener("click", () => {
        notificationTabs.querySelectorAll("[data-category]").forEach(item => item.classList.remove("active"));
        button.classList.add("active");
        const category = button.dataset.category;
        document.querySelectorAll("[data-notification-row]").forEach(row => {
          row.hidden = category !== "all" && row.dataset.category !== category;
        });
      });
    });
  }

  const preferences = document.querySelector("[data-notification-preferences]");
  if (preferences) {
    preferences.querySelectorAll("[data-pref]").forEach(input => {
      const key = `antam-notification-${input.dataset.pref}`;
      const saved = localStorage.getItem(key);
      if (saved !== null) input.checked = saved === "true";
      input.addEventListener("change", () => {
        localStorage.setItem(key, input.checked);
        const message = preferences.querySelector(".preference-saved");
        message.hidden = false;
        window.setTimeout(() => { message.hidden = true; }, 1400);
      });
    });
  }

  document.querySelectorAll("[data-print-page]").forEach(button => {
    button.addEventListener("click", () => window.print());
  });

  const paymentForm = document.querySelector("[data-payment-form]");
  if (paymentForm) {
    const options = [...paymentForm.querySelectorAll('input[name="method"]')];
    const details = [...paymentForm.querySelectorAll("[data-payment-detail]")];
    const submit = paymentForm.querySelector("[data-payment-submit]");
    const status = paymentForm.dataset.invoiceStatus;
    const amount = paymentForm.dataset.amount;
    if (status === "CashPending") {
      const cashOption = options.find(option => option.value === "Cash");
      if (cashOption) cashOption.checked = true;
    }

    const updatePaymentMethod = () => {
      const method = options.find(option => option.checked)?.value || "BankQR";
      details.forEach(detail => {
        detail.hidden = detail.dataset.paymentDetail !== method;
        detail.classList.toggle("active", !detail.hidden);
      });

      if (status === "Paid" || status === "Cancelled") return;
      if (method === "Cash") {
        submit.textContent = status === "CashPending"
          ? "Đã đăng ký thanh toán tại quầy"
          : "Đăng ký thanh toán tiền mặt";
        submit.disabled = status === "CashPending";
      } else {
        submit.textContent = `Tôi đã chuyển khoản · ${amount} đ`;
        submit.disabled = false;
      }
    };

    options.forEach(option => option.addEventListener("change", updatePaymentMethod));
    paymentForm.addEventListener("submit", event => {
      const method = options.find(option => option.checked)?.value;
      const message = method === "Cash"
        ? "Đăng ký thanh toán tiền mặt tại quầy?"
        : "Bạn xác nhận đã chuyển khoản đúng số tiền trên mã QR?";
      if (!window.confirm(message)) event.preventDefault();
    });
    updatePaymentMethod();
  }

  document.querySelectorAll(".chat-upload input").forEach(chatUpload => {
    chatUpload.addEventListener("change", () => {
      const label = chatUpload.closest(".chat-upload");
      label.classList.toggle("has-file", chatUpload.files.length > 0);
      label.title = chatUpload.files[0]?.name || "Gửi hình ảnh";
    });
  });

  const chatMessages = document.querySelector("[data-chat-messages]");
  if (chatMessages) chatMessages.scrollTop = chatMessages.scrollHeight;

  const floatingChat = document.querySelector("[data-floating-chat]");
  if (floatingChat) {
    const toggle = floatingChat.querySelector("[data-chat-toggle]");
    const close = floatingChat.querySelector("[data-chat-close]");
    const panel = floatingChat.querySelector(".floating-chat-panel");
    const form = floatingChat.querySelector("[data-floating-chat-form]");
    const input = form?.querySelector('input[name="message"]');
    const messages = floatingChat.querySelector("[data-floating-chat-messages]");

    const setOpen = open => {
      panel.hidden = !open;
      toggle.setAttribute("aria-expanded", String(open));
      if (open) input?.focus();
    };

    const appendMessage = (text, type, label) => {
      const item = document.createElement("div");
      item.className = `message ${type}`;
      const body = document.createElement("p");
      body.textContent = text;
      const meta = document.createElement("small");
      meta.textContent = label;
      item.append(body, meta);
      messages.appendChild(item);
      messages.scrollTop = messages.scrollHeight;
      return item;
    };

    toggle.addEventListener("click", () => setOpen(panel.hidden));
    close.addEventListener("click", () => setOpen(false));

    form?.addEventListener("submit", async event => {
      event.preventDefault();
      const text = input.value.trim();
      if (!text) return;
      appendMessage(text, "outgoing", "Bạn");
      const formData = new FormData(form);
      formData.set("message", text);
      input.value = "";
      const pending = appendMessage("\u0110ang ph\u00e2n t\u00edch Knowledge Graph v\u00e0 t\u1ea1o ph\u1ea3n h\u1ed3i...", "incoming", "AI");

      try {
        const response = await fetch(form.action, {
          method: "POST",
          body: formData,
          headers: { "X-Requested-With": "XMLHttpRequest" }
        });
        const data = await response.json();
        pending.querySelector("p").textContent = data.reply || "Tôi chưa thể trả lời lúc này. Bạn thử lại sau nhé.";
      } catch {
        pending.querySelector("p").textContent = "Kết nối chat đang gián đoạn. Bạn thử lại sau nhé.";
      }
    });
  }

  const adminChatMessages = document.querySelector("[data-admin-chat-messages]");
  if (adminChatMessages) adminChatMessages.scrollTop = adminChatMessages.scrollHeight;

  const adminChatSearch = document.querySelector("[data-admin-chat-search]");
  if (adminChatSearch) {
    adminChatSearch.addEventListener("input", () => {
      const query = adminChatSearch.value.trim().toLowerCase();
      document.querySelectorAll("[data-admin-conversation]").forEach(item => {
        item.hidden = Boolean(query) && !item.dataset.searchText.includes(query);
      });
    });
  }

  const wirePharmacyFilter = (searchId, filterId, rowSelector) => {
    const search = document.getElementById(searchId);
    const filter = document.getElementById(filterId);
    const rows = [...document.querySelectorAll(rowSelector)];
    if (!rows.length || (!search && !filter)) return;

    const apply = () => {
      const query = (search?.value || "").trim().toLowerCase();
      const state = filter?.value || "all";
      rows.forEach(row => {
        const matchesQuery = !query || (row.dataset.search || "").toLowerCase().includes(query);
        const matchesState = state === "all" || row.dataset.state === state;
        row.hidden = !matchesQuery || !matchesState;
      });
    };

    search?.addEventListener("input", apply);
    filter?.addEventListener("change", apply);
    apply();
  };

  wirePharmacyFilter("receiptSearch", null, "[data-receipt-row]");
  wirePharmacyFilter("lotSearch", "lotFilter", "[data-lot-row]");
  wirePharmacyFilter("transactionSearch", "transactionFilter", "[data-transaction-row]");
  wirePharmacyFilter("expirySearch", "expiryFilter", "[data-expiry-row]");
});
