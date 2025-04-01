"use client"

import { createContext, useContext, type ReactNode, useEffect, useState } from "react";
import { GetNodeService } from "../IpcService/INodeService";
import { UiService } from "../IpcService/UiService";

interface BackendContextType {
    isConnected: boolean;
    pickFile: () => Promise<string>;
    pickFolder: () => Promise<string>;
    importFile: (path: string) => Promise<void>;
    publishToTracker: (hashes: string[], trackerUri: string) => Promise<void>;
}

const BackendContext = createContext<BackendContextType | undefined>(undefined);

export function useBackend() {
    const context = useContext(BackendContext);
    if (context === undefined) {
        throw new Error("useBackend must be used within a BackendProvider");
    }
    return context;
}

interface BackendProviderProps {
    children: ReactNode;
}

export function BackendProvider({ children }: BackendProviderProps) {
    const [isConnected, setIsConnected] = useState(false);
    const [uiService, setUiService] = useState<UiService | null>(null);

    useEffect(() => {
        const initBackend = async () => {
            try {
                const nodeService = await GetNodeService();
                const service = new UiService(nodeService);
                setUiService(service);
                setIsConnected(true);
            } catch (error) {
                console.error("Failed to connect to backend:", error);
                setIsConnected(false);
            }
        };

        initBackend();
    }, []);

    const pickFile = () => uiService?.pickFile() ?? Promise.reject("Service not initialized");
    const pickFolder = () => uiService?.pickFolder() ?? Promise.reject("Service not initialized");
    const importFile = (path: string) => uiService?.importFile(path) ?? Promise.reject("Service not initialized");
    const publishToTracker = (hashes: string[], trackerUri: string) => uiService?.publishToTracker(hashes, trackerUri) ?? Promise.reject("Service not initialized");

    const value = {
        isConnected,
        pickFile,
        pickFolder,
        importFile,
        publishToTracker,
    };

    return <BackendContext.Provider value={value}>{children}</BackendContext.Provider>;
}
