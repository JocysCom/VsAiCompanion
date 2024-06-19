# Suggested Names Following Microsoft's Naming and Abbreviation Guidelines
# grp-ai-risk-level-low
# grp-ai-risk-level-medium
# grp-ai-risk-level-high
# grp-ai-risk-level-critical

data "azuread_group" "g2" {
  display_name     = "AI_RiskLevel_Low"
  security_enabled = true
}

data "azuread_group" "g3" {
  display_name     = "AI_RiskLevel_Medium"
  security_enabled = true
}

data "azuread_group" "g4" {
  display_name     = "AI_RiskLevel_High"
  security_enabled = true
}

data "azuread_group" "g5" {
  display_name     = "AI_RiskLevel_Critical"
  security_enabled = true
}

