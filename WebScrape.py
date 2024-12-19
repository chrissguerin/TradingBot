import requests
import json
import sys
import io

# Optional: Reconfigure stdout to handle UTF-8 characters
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

# Define the GraphQL endpoint
url = 'https://api.csgoroll.com/graphql'

# Set up headers. Modify or add headers as necessary.
headers = {
    'User-Agent': 'Mozilla/5.0',
    'Content-Type': 'application/json',
    'Accept': 'application/json',
    # 'Authorization': 'Bearer YOUR_ACCESS_TOKEN',  # Uncomment and set if authentication is required
}

# Define the persisted query hash and operation name
persisted_query = {
    "version": 1,
    "sha256Hash": "0f3a1ea7529016eaa9d8daee8fa24661e437d1a80d81670003b9484dc47bcb4c"
}

operation_name = "TradeList"

# Define variables for the query
variables = {
    "first": 50,
    "orderBy": "BEST_DEALS",          # Ordering by BEST_DEALS
    "status": "LISTED",
    "steamAppName": "CSGO",
    "t": "1734586625135",             # Timestamp or other required parameter
    "after": "WzQ2OTg3NTc2LDQ5XQ=="  # Pagination cursor; set to None or omit for the first request
}

# Construct the payload
payload = {
    "operationName": operation_name,
    "variables": variables,
    "extensions": {
        "persistedQuery": persisted_query
    }
}

def fetch_trades(after_cursor=None):
    """Fetch trades from the CSGORoll GraphQL API ordered by BEST_DEALS."""
    
    # Update the 'after' cursor if provided
    if after_cursor:
        payload['variables']['after'] = after_cursor
    else:
        payload['variables'].pop('after', None)  # Remove 'after' if it's the first request

    try:
        response = requests.post(url, headers=headers, data=json.dumps(payload))
    except requests.exceptions.RequestException as e:
        print(f"Request failed: {e}")
        return None

    if response.status_code == 200:
        try:
            return response.json()
        except json.JSONDecodeError:
            print("Failed to decode JSON response")
            return None
    else:
        print(f"Query failed with status code {response.status_code}: {response.text}")
        return None

def process_trades(data):
    """Process and print trade information from the response data."""
    try:
        trades = data['data']['trades']['edges']
        for trade in trades:
            trade_id = trade['node']['id']
            print(f"Trade ID: {trade_id}")
            for item in trade['node']['tradeItems']:
                market_name = item.get('marketName', 'N/A')
                print(f" - {market_name}")
        # Retrieve the 'after' cursor for pagination
        page_info = data['data']['trades']['pageInfo']
        return page_info.get('endCursor')
    except KeyError as e:
        print(f"Unexpected response structure: Missing key {e}")
        print(json.dumps(data, indent=2))
        return None

def main():
    """Main function to fetch and process trades."""
    after_cursor = None  # Initialize cursor
    while True:
        data = fetch_trades(after_cursor)
        if not data:
            break
        after_cursor = process_trades(data)
        if not after_cursor:
            break  # No more pages
        print("\nFetching next page...\n")

if __name__ == "__main__":
    main()
