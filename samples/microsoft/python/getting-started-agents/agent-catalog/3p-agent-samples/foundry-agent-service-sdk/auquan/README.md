# AuquanRiskAnalyzer

The Due Diligence Risk Analyst is an expert system designed to provide comprehensive risk analysis and timeline tracking for companies. It specializes in analyzing company risks across multiple dimensions including operational, financial, regulatory, and sustainability metrics. The agent processes structured risk data from Auquan's API, generates detailed timelines, and provides actionable insights through well-formatted reports with visual risk indicators.

---

## 💼 Use Cases
1. Comprehensive Risk Analysis
   - "Do a risk analysis for Total energies"

2. Specific Risk Assessment
   - "What are the critical risks identified for TotalEnergies?"

3. Sustainability Analysis
   - "Generate a sustainability analysis for TotalEnergies"

4. Risk Table Creation
   - "Create a table indicating risks for TotalEnergies showing all categories and severity"

5. Overall Risk Rating Analysis
   - "What is the overall risk range of TotalEnergies?"

6. Recent Theme Analysis
   - "What are the recent themes around TotalEnergies and what are their impacts?"

---

## 🧩 Tools

This agent leverages **Azure AI Agent Service**, using the following tools:
## Tool 1: Auquan API
**Description:** 
Retrieves and processes company risk data from Auquan's API. This tool enables the agent to access comprehensive company information, risk assessments, and thematic analysis data.

**API Endpoint:** `https://agents.auquan.com/api/analyze-query`
**Authentication:** API key in x-api-key header
**Request Format:**
```json
{
  "query": "do a risk analysis for {COMPANY_NAME}"
}
```

**Error Handling:**
- 401: Check API key validity
- 500: Retry with backoff
- Log all errors for monitoring

### Tool 2: Code Interpreter
**Description:**
Performs data analysis and visualization tasks including:
- Risk score calculations
- Timeline generation
- Data formatting and structuring
- Table and chart creation

**Reference Files:**
- No specific reference files required
- No authentication required

### Tool 3: Grounding with Bing Search
**Description:**
Enriches the analysis with:
- Latest news and developments
- Regulatory updates
- Industry trends
- Sustainability initiatives

**Reference Files:**
- No specific reference files required
- No authentication required

The agent is configured via a `template.py` file and deployable with Bicep for enterprise use.


## ⚙️ Setup Instructions

### Prerequisites
Obtain an API key for your Auquan Risk Agent.

---
## 💬 Example Agent Interactions

- "Do a risk analysis for Total energies"

- "What are the critical risks identified for TotalEnergies?"

- "Generate a sustainability analysis for TotalEnergies"

- "Create a table indicating risks for TotalEnergies showing all categories and severity"

- "What is the overall risk range of TotalEnergies?"

- "What are the recent themes around TotalEnergies and what are their impacts?"

 
## 🛠 Customization Tips

- **Connect with more datasets**  
  Add datasets like lawsuits/sanctions to the agent's analysis via the Auquan Knowledge tool to identify potential risks.

- **Connect with vector stores**  
  Connect with vector stores that have specific data (eg. Annual Reports, Industry Reports etc.) ingested into them.

- **Connect with local files**  
  Add your local files to include your data in risk-analysis