// Define interfaces for the backend services
export interface BackendServiceInterface {
    publishToTracker: (containerId: string, trackerUri: string) => Promise<boolean>
    downloadContainer: (containerGuid: string, trackerUri: string) => Promise<boolean>
    fetchDriveData: () => Promise<DriveData>
}

// Define the data structure
export interface DriveData {
    files: File[]
    folders: Folder[]
}

// Import the types and mock data
import type { File, Folder } from "../lib/types"
import { mockFiles, mockFolders } from "../lib/mock-data"

// Mock implementation for development
class BackendServiceMock implements BackendServiceInterface {
    async publishToTracker(containerId: string, trackerUri: string): Promise<boolean> {
        console.log(`[MOCK] Publishing container ${containerId} to tracker ${trackerUri}`)
        // Simulate network delay
        await new Promise((resolve) => setTimeout(resolve, 1000))
        return true
    }

    async downloadContainer(containerGuid: string, trackerUri: string): Promise<boolean> {
        console.log(`[MOCK] Downloading container ${containerGuid} from tracker ${trackerUri}`)
        // Simulate network delay
        await new Promise((resolve) => setTimeout(resolve, 1000))
        return true
    }

    async fetchDriveData(): Promise<DriveData> {
        console.log("[MOCK] Fetching drive data")

        // Simulate network delay (between 500ms and 1500ms)
        const delay = Math.floor(Math.random() * 1000) + 500
        await new Promise((resolve) => setTimeout(resolve, delay))

        // Occasionally add a new random file or folder (10% chance)
        if (Math.random() < 0.1) {
            const isFile = Math.random() > 0.5

            if (isFile) {
                const fileTypes = ["document", "spreadsheet", "image", "other"] as const
                const fileType = fileTypes[Math.floor(Math.random() * fileTypes.length)]
                const newFile: File = {
                    id: `file-${Date.now()}`,
                    name: `New ${fileType} ${new Date().toLocaleTimeString()}.${fileType === "document" ? "docx" : fileType === "spreadsheet" ? "xlsx" : fileType === "image" ? "jpg" : "txt"}`,
                    type: fileType,
                    size: Math.floor(Math.random() * 5000000) + 100000,
                    folderId: null,
                    createdAt: new Date().toISOString(),
                    modifiedAt: new Date().toISOString(),
                    thumbnail: fileType === "image" ? "/placeholder.svg?height=100&width=100" : undefined,
                }
                mockFiles.push(newFile)
                console.log("[MOCK] Added new file:", newFile.name)
            } else {
                const newFolder: Folder = {
                    id: `folder-${Date.now()}`,
                    name: `New Folder ${new Date().toLocaleTimeString()}`,
                    parentId: null,
                    createdAt: new Date().toISOString(),
                    modifiedAt: new Date().toISOString(),
                    hasChildren: false,
                }
                mockFolders.push(newFolder)
                console.log("[MOCK] Added new folder:", newFolder.name)
            }
        }

        return {
            files: [...mockFiles], // Return a copy to avoid reference issues
            folders: [...mockFolders],
        }
    }
}

// Export a singleton instance
export const backendService: BackendServiceInterface = new BackendServiceMock()

