{{/*
Expand the name of the chart.
*/}}
{{- define "ragframework.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
*/}}
{{- define "ragframework.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "ragframework.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "ragframework.labels" -}}
helm.sh/chart: {{ include "ragframework.chart" . }}
{{ include "ragframework.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
Selector labels
*/}}
{{- define "ragframework.selectorLabels" -}}
app.kubernetes.io/name: {{ include "ragframework.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "ragframework.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "ragframework.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{/*
Generate full image name for API
*/}}
{{- define "ragframework.api.image" -}}
{{- $registry := .Values.image.registry }}
{{- $repo := .Values.image.repository }}
{{- $name := .Values.api.image.name }}
{{- $tag := .Values.global.imageTag | default "latest" }}
{{- printf "%s/%s/%s:%s" $registry $repo $name $tag }}
{{- end }}

{{/*
Generate full image name for Embedding service
*/}}
{{- define "ragframework.embedding.image" -}}
{{- $registry := .Values.image.registry }}
{{- $repo := .Values.image.repository }}
{{- $name := .Values.embedding.image.name }}
{{- $tag := .Values.global.imageTag | default "latest" }}
{{- printf "%s/%s/%s:%s" $registry $repo $name $tag }}
{{- end }}

{{/*
Generate full image name for Migrations
*/}}
{{- define "ragframework.migrations.image" -}}
{{- $registry := .Values.image.registry }}
{{- $repo := .Values.image.repository }}
{{- $name := .Values.migrations.image.name }}
{{- $tag := .Values.global.imageTag | default "latest" }}
{{- printf "%s/%s/%s:%s" $registry $repo $name $tag }}
{{- end }}

{{/*
Component specific labels for API
*/}}
{{- define "ragframework.api.labels" -}}
{{ include "ragframework.labels" . }}
app.kubernetes.io/component: api
{{- end }}

{{/*
Component specific labels for Embedding
*/}}
{{- define "ragframework.embedding.labels" -}}
{{ include "ragframework.labels" . }}
app.kubernetes.io/component: embedding
{{- end }}

{{/*
Component specific labels for Migrations
*/}}
{{- define "ragframework.migrations.labels" -}}
{{ include "ragframework.labels" . }}
app.kubernetes.io/component: migrations
{{- end }}

{{/*
Component selector labels for API
*/}}
{{- define "ragframework.api.selectorLabels" -}}
{{ include "ragframework.selectorLabels" . }}
app.kubernetes.io/component: api
{{- end }}

{{/*
Component selector labels for Embedding
*/}}
{{- define "ragframework.embedding.selectorLabels" -}}
{{ include "ragframework.selectorLabels" . }}
app.kubernetes.io/component: embedding
{{- end }}
