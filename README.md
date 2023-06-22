# DropRetentionLeases

## Overview

Simple command line utility in C#/.NET that uses Azure DevOps REST API to delete retention leases from Azure Pipelines build in bulk.

## Usage

To delete all retention leases on a pipeline, run:

```
DropRetentionLeases --Organization msazure --Project One --BuildId 77696 --Pat gku3xaljkljwtsqkcuy4djtlycll7gvqyzb3rkvt5hgxbdh3ufq
```

where `Pat` is a Personal Access Token (PAT) that you need to generate in Azure DevOps first in order to use Azure DevOps API.