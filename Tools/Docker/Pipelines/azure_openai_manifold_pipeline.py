"""
title: Azure OpenAI Manifold Pipeline
author: OpenWebUI
date: 2024-06-20
version: 1.2
license: MIT
description: A pipeline for generating text using the Azure OpenAI API.
requirements: requests
environment_variables: AZURE_OPENAI_API_KEY, AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_API_VERSION, AZURE_OPENAI_MODELS, AZURE_OPENAI_MODEL_NAMES
"""

from typing import List, Union, Generator, Iterator
from pydantic import BaseModel
import requests
import os


class Pipeline:
    class Valves(BaseModel):
        # You can add your custom valves here.
        AZURE_OPENAI_API_KEY: str
        AZURE_OPENAI_ENDPOINT: str
        AZURE_OPENAI_API_VERSION: str
        AZURE_OPENAI_MODELS: str
        AZURE_OPENAI_MODEL_NAMES: str

    def __init__(self):
        self.type = "manifold"
        # Add ID field for consistency with other manifold pipelines
        self.id = "azure-openai" 
        self.name = "Azure OpenAI: "
        self.valves = self.Valves(
            **{
                "AZURE_OPENAI_API_KEY": os.getenv("AZURE_OPENAI_API_KEY", "your-azure-openai-api-key-here"),
                "AZURE_OPENAI_ENDPOINT": os.getenv("AZURE_OPENAI_ENDPOINT", "your-azure-openai-endpoint-here"),
                "AZURE_OPENAI_API_VERSION": os.getenv("AZURE_OPENAI_API_VERSION", "2024-02-01"),
                "AZURE_OPENAI_MODELS": os.getenv("AZURE_OPENAI_MODELS", "gpt-35-turbo;gpt-4o"),
                "AZURE_OPENAI_MODEL_NAMES": os.getenv("AZURE_OPENAI_MODEL_NAMES", "GPT-35 Turbo;GPT-4o"),
            }
        )
        self._models = self._get_models()

    def _get_models(self):
        """Internal method to get available models"""
        models = self.valves.AZURE_OPENAI_MODELS.split(";")
        model_names = self.valves.AZURE_OPENAI_MODEL_NAMES.split(";")
        return [
            {"id": model, "name": name} for model, name in zip(models, model_names)
        ]

    # Add pipelines method to match other manifold pipelines interface
    def pipelines(self) -> List[dict]:
        """Return available models that can be used"""
        return self._models

    async def on_valves_updated(self):
        """Update models when valves are updated"""
        self._models = self._get_models()

    async def on_startup(self):
        # This function is called when the server is started.
        print(f"on_startup:{__name__}")
        pass

    async def on_shutdown(self):
        # This function is called when the server is stopped.
        print(f"on_shutdown:{__name__}")
        pass

    def pipe(
            self, user_message: str, model_id: str, messages: List[dict], body: dict
    ) -> Union[str, Generator, Iterator]:
        # This is where you can add your custom pipelines like RAG.
        print(f"pipe:{__name__}")

        # Create a copy of body to avoid modifying the original
        processed_body = body.copy()

        headers = {
            "api-key": self.valves.AZURE_OPENAI_API_KEY,
            "Content-Type": "application/json",
        }

        url = f"{self.valves.AZURE_OPENAI_ENDPOINT}/openai/deployments/{model_id}/chat/completions?api-version={self.valves.AZURE_OPENAI_API_VERSION}"

        allowed_params = {'messages', 'temperature', 'role', 'content', 'contentPart', 'contentPartImage',
                          'enhancements', 'dataSources', 'n', 'stream', 'stop', 'max_tokens', 'presence_penalty',
                          'frequency_penalty', 'logit_bias', 'user', 'function_call', 'funcions', 'tools',
                          'tool_choice', 'top_p', 'log_probs', 'top_logprobs', 'response_format', 'seed'}
        # remap user field
        if "user" in processed_body and not isinstance(processed_body["user"], str):
            processed_body["user"] = processed_body["user"]["id"] if "id" in processed_body["user"] else str(processed_body["user"])
        filtered_body = {k: v for k, v in processed_body.items() if k in allowed_params}
        # log fields that were filtered out as a single line
        if len(processed_body) != len(filtered_body):
            print(f"Dropped params: {', '.join(set(processed_body.keys()) - set(filtered_body.keys()))}")

        try:
            r = requests.post(
                url=url,
                json=filtered_body,
                headers=headers,
                stream=True,
            )

            r.raise_for_status()
            if processed_body.get("stream", False):
                return r.iter_lines()
            else:
                return r.json()
        except Exception as e:
            error_text = getattr(r, 'text', 'No response')
            return f"Error: {e} ({error_text})"