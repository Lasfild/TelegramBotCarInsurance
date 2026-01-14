Overview

This project is a Telegram bot that assists users in purchasing car insurance.
The bot guides the user through document submission, extracts relevant data using AI-powered services, confirms the extracted information, and finally issues a dummy insurance policy.

The solution is designed with clean architecture principles, clear separation of concerns, and external AI integrations.

-----

Key Features

Telegram bot built using Telegram.Bot API

Step-by-step user flow with session state management

Document recognition using Mindee API

Passport data extraction

Vehicle registration document extraction

AI-powered conversational responses and policy generation using Groq (OpenAI-compatible API)

Clear confirmation steps after each document

Dummy insurance policy generation (text-based)

Robust error handling and user-friendly messages

-----

Bot Workflow

Start

User sends /start

Bot explains the purpose and asks for a passport photo

Passport Processing

User uploads passport photo

Mindee extracts:

First name(s)

Last name

Document number

Bot asks the user to confirm the extracted data

Vehicle Document Processing

User uploads vehicle registration document

Mindee extracts:

Vehicle brand/model

License plate number

Bot asks the user to confirm the extracted data

Price Confirmation

Bot informs user about the fixed price: 100 USD

User confirms or declines

Policy Issuance

AI generates a dummy insurance policy

Bot sends the policy to the user

-----

AI Integrations
Groq (OpenAI-Compatible API)

Used for:

Natural language communication

Dummy insurance policy generation

Reasoning and explanations are explicitly disabled

Output is strictly formatted and user-facing

Mindee API

Used for document recognition

Two separate Custom Extraction Models:

Passport model

Vehicle registration document model

One API key is shared across both models

-----

Configuration

Create an appsettings.json file using appsettings.example.json as example

-----

How to Run

Clone the repository

Restore dependencies:

dotnet restore


Configure appsettings.json

Run the project:

dotnet run


Open your Telegram bot and send /start

-----

Example Interaction

tg_bot_DICEUS_test_task, [14.01.2026 15:51]
Hello! I will help you purchase car insurance.
Please send a photo of your PASSPORT to get started.

*sends passport*

tg_bot_DICEUS_test_task, [14.01.2026 15:51]
Analyzing passport…

tg_bot_DICEUS_test_task, [14.01.2026 15:51]
Passport data detected:
First name(s): МАР'ЯНА MARIANA
Last name: ТКАЧЕНКО TKACHENKO
Document number: 000000000

Is this information correct? (Yes / No)

Denys, [14.01.2026 15:51]
yes

tg_bot_DICEUS_test_task, [14.01.2026 15:51]
Great! Now please send a photo of your VEHICLE REGISTRATION DOCUMENT.

*sends vehicle document*

tg_bot_DICEUS_test_task, [14.01.2026 15:51]
Analyzing vehicle document…

tg_bot_DICEUS_test_task, [14.01.2026 15:51]
Vehicle data detected:
Vehicle model: VOLKSWAGEN ЛЕГКОВИЙ ХЕТЧБЕК-В
License plate: 159909

Is this information correct? (Yes / No)

Denys, [14.01.2026 15:51]
yes

tg_bot_DICEUS_test_task, [14.01.2026 15:51]
The insurance price is fixed at 100 USD.
Do you agree? (Yes / No)

Denys, [14.01.2026 15:51]
yes

tg_bot_DICEUS_test_task, [14.01.2026 15:51]
Issuing your insurance policy…

tg_bot_DICEUS_test_task, [14.01.2026 15:51]
=== CAR INSURANCE POLICY #98851 ===
Policyholder: МАР'ЯНА MARIANA ТКАЧЕНКО TKACHENKO
Vehicle: VOLKSWAGEN ЛЕГКОВИЙ ХЕТЧБЕК-В
License Plate: 159909
Amount: 100 USD
Status: PAID
Date: 2026-01-14
==========================================

tg_bot_DICEUS_test_task, [14.01.2026 15:51]
Thank you for your purchase!
