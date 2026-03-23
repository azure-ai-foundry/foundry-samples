# Azure Policy Definitions for Microsoft Foundry

This folder contains examples for implementing custom Azure Policy definitions to govern Microsoft Foundry deployments.

## Overview

Azure Policy helps enforce organizational standards and assess compliance at scale. Custom policy definitions can restrict certain connection types or enforce specific configurations.

## Example Policies

- Deny disallowed connection types
- Enforce private endpoints
- Require customer-managed keys
- Mandate specific networking configurations

## Prerequisites

- Policy Contributor role or equivalent permissions
- Understanding of Azure Policy definition structure

## Usage

Define policy rules using Azure Policy definition syntax. Apply policies at subscription or resource group scope.

## Status

This folder now includes complete Terraform policy samples with:
- Custom policy definitions for connection category controls
- Optional subscription policy assignment
- Additional policy examples for auth and account-kind governance

## Example Reference

The Bicep folder contains a `deny-disallowed-connections.json` policy that can be adapted to Terraform.

## Documentation

- [Azure Policy overview](https://learn.microsoft.com/en-us/azure/governance/policy/overview)
- [Azure Policy definition structure](https://learn.microsoft.com/en-us/azure/governance/policy/concepts/definition-structure)
- [azurerm_policy_definition - Terraform](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/policy_definition)

`Tags: Azure Policy, Governance, Compliance`
