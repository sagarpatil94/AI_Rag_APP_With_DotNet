const documentForm = document.querySelector("#documentForm");
const sourceInput = document.querySelector("#sourceInput");
const fileInput = document.querySelector("#fileInput");
const contentInput = document.querySelector("#contentInput");
const characterCount = document.querySelector("#characterCount");
const ingestStatus = document.querySelector("#ingestStatus");
const addDocumentButton = document.querySelector("#addDocumentButton");
const chatForm = document.querySelector("#chatForm");
const questionInput = document.querySelector("#questionInput");
const topKInput = document.querySelector("#topKInput");
const askButton = document.querySelector("#askButton");
const chatMessages = document.querySelector("#chatMessages");
const healthDot = document.querySelector("#healthDot");
const healthText = document.querySelector("#healthText");

async function requestJson(url, body) {
  const response = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body)
  });
  const responseText = await response.text();
  let data;

  try {
    data = responseText ? JSON.parse(responseText) : null;
  } catch {
    data = null;
  }

  if (!response.ok) {
    const message = data?.error || data?.detail || `Request failed (${response.status}).`;
    throw new Error(message);
  }

  if (data === null) {
    throw new Error("The API returned an unreadable response.");
  }

  return data;
}

function setBusy(button, busy, label) {
  button.disabled = busy;
  button.setAttribute("aria-busy", String(busy));
  const buttonLabel = button.querySelector(".button-label, .send-label");
  if (buttonLabel) {
    if (busy) {
      button.dataset.originalLabel = buttonLabel.textContent;
      buttonLabel.textContent = label;
    } else {
      buttonLabel.textContent = button.dataset.originalLabel || label;
    }
  }
}

function setIngestStatus(message, kind = "") {
  ingestStatus.textContent = message;
  ingestStatus.className = `inline-status${kind ? ` ${kind}` : ""}`;
}

function scrollChatToBottom() {
  chatMessages.scrollTop = chatMessages.scrollHeight;
}

function createMessage(role, text, options = {}) {
  const message = document.createElement("article");
  message.className = `chat-message ${role}${options.error ? " chat-error" : ""}`;

  if (role === "assistant") {
    const avatar = document.createElement("div");
    avatar.className = "assistant-avatar";
    avatar.setAttribute("aria-hidden", "true");
    avatar.textContent = "✳";
    message.append(avatar);
  }

  const content = document.createElement("div");
  content.className = "message-content";

  const author = document.createElement("span");
  author.className = "message-author";
  author.textContent = role === "user" ? "YOU" : "RAG ASSISTANT";

  const paragraph = document.createElement("p");
  paragraph.textContent = text;
  content.append(author, paragraph);

  if (options.loading) {
    const indicator = document.createElement("span");
    indicator.className = "typing-indicator";
    indicator.setAttribute("aria-label", "Thinking");
    indicator.innerHTML = "<span></span><span></span><span></span>";
    content.append(indicator);
  }

  if (options.sources?.length) {
    const sources = document.createElement("div");
    sources.className = "sources";

    for (const source of options.sources) {
      const chip = document.createElement("span");
      chip.className = "source-chip";

      const name = document.createElement("span");
      name.className = "source-name";
      name.textContent = source.source;

      const score = document.createElement("span");
      score.className = "source-score";
      score.textContent = `${(source.score * 100).toFixed(1)}% match`;

      chip.append(name, score);
      sources.append(chip);
    }

    content.append(sources);
  }

  message.append(content);
  chatMessages.append(message);
  scrollChatToBottom();
  return message;
}

function resizeQuestionInput() {
  questionInput.style.height = "auto";
  questionInput.style.height = `${Math.min(questionInput.scrollHeight, 110)}px`;
}

fileInput.addEventListener("change", async () => {
  const [file] = fileInput.files || [];
  if (!file) {
    return;
  }

  try {
    contentInput.value = await file.text();
    sourceInput.value = file.name;
    characterCount.textContent = `${contentInput.value.length.toLocaleString()} characters`;
    setIngestStatus(`${file.name} is ready to add.`);
  } catch {
    setIngestStatus("Could not read that file. Try a plain text file.", "error");
  }
});

contentInput.addEventListener("input", () => {
  characterCount.textContent = `${contentInput.value.length.toLocaleString()} characters`;
});

documentForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  const source = sourceInput.value.trim();
  const content = contentInput.value.trim();

  if (!source || !content) {
    setIngestStatus("Add a document name and some text first.", "error");
    return;
  }

  setBusy(addDocumentButton, true, "Indexing…");
  setIngestStatus("Creating embeddings with your local model. This may take a moment.");

  try {
    const result = await requestJson("/api/documents", { source, content });
    setIngestStatus(
      `${result.source} added · ${result.chunks} searchable ${result.chunks === 1 ? "chunk" : "chunks"}.`,
      "success"
    );
    questionInput.focus();
  } catch (error) {
    setIngestStatus(error.message || "Could not add the document.", "error");
  } finally {
    setBusy(addDocumentButton, false, "Add to knowledge base");
  }
});

chatForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  const question = questionInput.value.trim();

  if (!question) {
    questionInput.focus();
    return;
  }

  createMessage("user", question);
  questionInput.value = "";
  resizeQuestionInput();
  setBusy(askButton, true, "...");
  const pendingMessage = createMessage("assistant", "Searching your documents…", { loading: true });

  try {
    const result = await requestJson("/api/chat", {
      question,
      topK: Number(topKInput.value)
    });
    createMessage("assistant", result.answer, { sources: result.sources });
    pendingMessage.remove();
    scrollChatToBottom();
  } catch (error) {
    pendingMessage.remove();
    createMessage("assistant", error.message || "Could not get an answer from the API.", { error: true });
  } finally {
    setBusy(askButton, false, "Ask");
    questionInput.focus();
  }
});

questionInput.addEventListener("input", resizeQuestionInput);
questionInput.addEventListener("keydown", (event) => {
  if (event.key === "Enter" && !event.shiftKey) {
    event.preventDefault();
    chatForm.requestSubmit();
  }
});

document.querySelectorAll(".suggestion").forEach((button) => {
  button.addEventListener("click", () => {
    questionInput.value = button.dataset.question || "";
    resizeQuestionInput();
    chatForm.requestSubmit();
  });
});

async function checkApiHealth() {
  try {
    const response = await fetch("/api/health");
    if (!response.ok) {
      throw new Error("API unavailable");
    }

    healthDot.className = "status-dot";
    healthText.textContent = "API connected";
  } catch {
    healthDot.className = "status-dot offline";
    healthText.textContent = "API unavailable";
  }
}

checkApiHealth();
