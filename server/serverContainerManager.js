class ServerContainerManager {
    constructor() {
        // Initialize your container with default values
        this.serverContainer = {};
    }

    // Method to update the server container with new data
    updateServerData(serverKey, newData) {
        // Implement logic to update the server container
        // For example, adding or updating a server's data
        if (!this.serverContainer[serverKey]) {
            this.serverContainer[serverKey] = {};
        }

        // Merge new data into the existing server data
        this.serverContainer[serverKey] = {
            ...this.serverContainer[serverKey],
            ...newData
        };
    }

    // Method to retrieve data from the server container
    getServerData(serverKey) {
        return this.serverContainer[serverKey] || null;
    }

    // Method to get all data (if needed)
    getAllData() {
        return this.serverContainer;
    }

    // Additional methods as needed for managing the state
}

export default ServerContainerManager;
