# 💼 Portfolio Navigator Agent

This code sample supports agent creation for exploring financial topics from Morningstar data and Bing Search, helps users explore investment products, understand market trends, and clarify financial concepts using trusted data sources. Built with Azure AI Agent Service, it integrates Bing Search and the Morningstar API to deliver transparent, cited insights—ideal for educational use, not personal financial advice.

**IMPORTANT NOTE:** Starter templates, instructions, code samples and resources in this msft-agent-samples file (“samples”) are designed to assist in accelerating development of agents for specific scenarios. It is important that you review all provided resources and carefully test Agent behavior in the context of your use case: ([Learn More](https://learn.microsoft.com/en-us/legal/cognitive-services/agents/transparency-note?context=%2Fazure%2Fai-services%2Fagents%2Fcontext%2Fcontext)). 

Certain Agent offerings may be subject to legal and regulatory requirements, may require licenses, or may not be suitable for all industries, scenarios, or use cases. By using any sample, you are acknowledging that Agents or other output created using that sample are solely your responsibility, and that you will comply with all applicable laws, regulations, and relevant safety standards, terms of service, and codes of conduct.  

---

## 🧰 Use Cases

- **Investment Research**: Fetch up-to-date metrics on ETFs, mutual funds, or market indices using Morningstar’s APIs.
- **Financial Concept Clarification**: Explain terms like “Sharpe Ratio,” “ESG,” or “fixed annuity” in simple language.
- **Market Sentiment Monitoring**: Summarize news and opinion pieces from Bing Search about specific sectors, companies, or economic events.
- **Client Prep for Advisors**: Pull up client preferences, prior notes, and regulatory docs to support meetings or CRM tasks.
- **Compliance Info Retrieval**: Answer common regulatory or product FAQ from internal content libraries.

---

## 🧩 Tools & Capabilities

Built with **Azure AI Agent Service**, the Portfolio Navigator Agent uses:

- **Morningstar API Tool**  
  Pulls trusted third-party data to show investment performance, fund ratings, expense ratios, and risk profiles.

- **Bing Search Tool**  
  Grounds answers in real-time public data, retrieves company profiles, market news, and headlines with citations.

- **File Search Tool**  
  Enables personalized, secure retrieval of:
  - Client profiles or interaction histories (e.g., CRM exports)
  - Internal product documentation (e.g., fixed deposits, annuities)
  - Educational guides or FAQ
  - Regulatory summaries or market briefs

---

## Setup Instructions

### Prerequisites

1. Azure subscription with the following permissions
   - Contributor or Cognitive Services Contributor role (for resource deployment)
   - Azure AI Developer and Cognitive Services user role (for agent creation)
2. Agent setup: deploy the latest agent setup using this ([custom deployment](https://www.aka.ms/basic-agent-deployment)).
   - The above creates:
      - AI Services resource
      - AI Project
      - Model deployment
3. Python 3.8+
4. Azure CLI
5. Azure AI Agent SDK installed (`requirements.txt`)
6. External API access credentials for Morningstar and Bing (where applicable)
7. Sample dataset: `trusty_link_dataset.xlsx`

---

## 💬 Example Agent Interactions

**User**: What’s the latest market news about Microsoft?  
**🌐 Response**: Bing Search retrieves recent news articles and headlines about Microsoft, grounded in real-time public data.

---

**User**: Can you show me the performance of the Vanguard Total Stock Market ETF?  
**📊 Response**: Morningstar API returns performance charts, volatility metrics, and key financial indicators for the ETF.

---

**User**: What are investors saying publicly about the real estate sector this quarter?  
**🗞️ Response**: Bing Search gathers recent articles, analyst commentary, and blog posts on real estate trends.

---

**User**: I want to understand how ESG funds are performing in 2024—can you help?  
**♻️ Response**: Morningstar API provides performance and comparison metrics for top ESG funds; Bing Search adds supporting commentary.

---

**User**: What is the Morningstar rating and risk profile of the Fidelity 500 Index Fund?  
**⭐ Response**: Morningstar API surfaces the fund's rating, Sharpe ratio, and benchmark comparisons.

---

## 🛠 Customization Tips

- **Plug Into CRM**: Add connectors to D365 or other CRMs to retrieve advisor calendars or appointment metadata.
- **Enable File Upload**: Use File Search to analyze uploaded fact sheets, earnings reports, or onboarding guides.
- **Add Industry Filters**: Tune prompts or ser queries to align recommendations with user-specified industry sectors.

