import React from "react"

import ReactDOM from "react-dom/client"

import { cleanLotsAfterDbReset } from "./lib/dbResetCleanup"

import "./index.css"
import "./theme.css"
import "./workspace-layout.css"
import "./ui-refinement.css"
import "./motion.css"
import "./adaptive-ui.css"

const isAssistantDemo = import.meta.env.DEV &&
  import.meta.env.VITE_ASSISTANT_DEMO_ENABLED === "true" &&
  window.location.pathname === "/demo/assistant"

async function mount() {
  if (import.meta.env.DEV && !isAssistantDemo) {
    await cleanLotsAfterDbReset()
  }
  const { default: Page } = isAssistantDemo
    ? await import("./components/chat/demo/AssistantDemoPage")
    : await import("./App")
  ReactDOM.createRoot(document.getElementById("root")!).render(
    <React.StrictMode><Page /></React.StrictMode>,
  )
}

void mount()
