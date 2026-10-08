# Chatbot integration scaffold

`Application/Chatbot/ChatbotIntegration.cs` contains configuration, context, model and backend ports, and the coordinator. It defaults to disabled. No real model provider, credentials or business data are bundled.

Adapters belong to Infrastructure and must call the owning backend API with current authorization. The initial allowlist contains FAQ and parking search only; writes hand off to the normal user-confirmed workflow. A model intent never grants a permission or determines pricing/payment/capacity. Provider configuration is deployment-specific. This task delivers the integration structure, not a functioning production chatbot.
