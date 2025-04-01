import { useBackend } from "../contexts/BackendContext"
import { Wifi, WifiOff } from "lucide-react"

export function ConnectionStatus() {
    const { isConnected } = useBackend()

    return (
        <div className="flex items-center gap-1 text-xs">
            {isConnected ? (
                <>
                    <Wifi className="h-3 w-3 text-green-500" />
                    <span className="text-green-500">Connected to backend</span>
                </>
            ) : (
                <>
                    <WifiOff className="h-3 w-3 text-red-500" />
                    <span className="text-red-500">Disconnected</span>
                </>
            )}
        </div>
    )
}

