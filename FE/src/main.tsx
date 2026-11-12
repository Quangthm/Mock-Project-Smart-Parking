import React from "react"

import ReactDOM from "react-dom/client"

import "./index.css"
import "./theme.css"
import "./workspace-layout.css"
import "./ui-refinement.css"
import "./motion.css"
import "./adaptive-ui.css"

const isAssistantDemo = import.meta.env.DEV &&
  import.meta.env.VITE_ASSISTANT_DEMO_ENABLED === "true" &&
  window.location.pathname === "/demo/assistant"

const page = isAssistantDemo
  ? import("./components/chat/demo/AssistantDemoPage")
  : import("./App")

void page.then(({ default: Page }) => {
  ReactDOM.createRoot(document.getElementById("root")!).render(
    <React.StrictMode><Page /></React.StrictMode>,
  )
})
