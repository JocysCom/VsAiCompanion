from typing import List, Union, Generator, Iterator, Optional
from pprint import pprint
import requests, json, warnings
import os
from pydantic import BaseModel

# Uncomment to disable SSL verification warnings if needed.
# warnings.filterwarnings('ignore', message='Unverified HTTPS request')

class Pipeline:
    class Valves(BaseModel):
        N8N_API_URL: str = ""
        N8N_API_KEY: str = ""

    def __init__(self):
        self.name = "N8N Agent Pipeline"
        
        # Use valves for configuration
        self.valves = self.Valves(
            **{
                "N8N_API_URL": os.getenv("N8N_API_URL", "https://n8n.jocys.com/webhook-test/nexusai"),
                "N8N_API_KEY": os.getenv("N8N_API_KEY", "")
            }
        )
        
        self.verify_ssl = True
        self.debug = False
        # Please note that N8N do not support stream reponses

    async def on_startup(self):
        # This function is called when the server is started.
        print(f"on_startup: {__name__}")
        pass
    
    async def on_shutdown(self): 
        # This function is called when the server is shutdown.
        print(f"on_shutdown: {__name__}")
        pass

    async def on_valves_updated(self):
        # This method is called when valves are updated through the UI
        print(f"on_valves_updated: {__name__}")
        pass

    async def inlet(self, body: dict, user: Optional[dict] = None) -> dict:
        # This function is called before the OpenAI API request is made. You can modify the form data before it is sent to the OpenAI API.
        print(f"inlet: {__name__}")
        if self.debug:
            print(f"inlet: {__name__} - body:")
            pprint(body)
            print(f"inlet: {__name__} - user:")
            pprint(user)
        return body

    async def outlet(self, body: dict, user: Optional[dict] = None) -> dict:
        # This function is called after the OpenAI API response is completed. You can modify the messages after they are received from the OpenAI API.
        print(f"outlet: {__name__}")
        if self.debug:
            print(f"outlet: {__name__} - body:")
            pprint(body)
            print(f"outlet: {__name__} - user:")
            pprint(user)
        return body

    def pipe(self, user_message: str, model_id: str, messages: List[dict], body: dict) -> Union[str, Generator, Iterator]:
        # This is where you can add your custom pipelines like RAG.
        print(f"pipe: {__name__}")
        
        if self.debug:
            print(f"pipe: {__name__} - received message from user: {user_message}")
        
        # This function triggers the workflow using the specified API.
        headers = {
            'Authorization': f'Bearer {self.valves.N8N_API_KEY}',
            'Content-Type': 'application/json'
        }
        
        # Get user email safely
        user_email = "anonymous"
        if "user" in body and isinstance(body["user"], dict) and "email" in body["user"]:
            user_email = body["user"]["email"]
            
        # Extract access_code if present in the request body
        access_code = body.get("access_code", None)

        # Prepare inputs with optional access_code
        inputs = {"prompt": user_message}
        if access_code is not None:
            inputs["access_code"] = access_code

        data = {
            "inputs": inputs,
            "user": user_email
        }

        response = requests.post(self.valves.N8N_API_URL, headers=headers, json=data, verify=self.verify_ssl)
        if response.status_code == 200:
            # Process and yield each chunk from the response
            try:
                for line in response.iter_lines():
                    if line:
                        # Decode each line assuming UTF-8 encoding and directly parse it as JSON
                        json_data = json.loads(line.decode('utf-8'))
                        # Check if 'output' exists in json_data and yield it
                        if 'output' in json_data:
                            yield json_data['output']
            except json.JSONDecodeError as e:
                print(f"Failed to parse JSON from line. Error: {str(e)}")
                yield "Error in JSON parsing."
        else:
            yield f"Workflow request failed with status code: {response.status_code}"