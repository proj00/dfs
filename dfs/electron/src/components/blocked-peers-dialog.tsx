"use client";

import { useState, useEffect } from "react";
import { Address4, Address6 } from "ip-address";
import {
  Shield,
  Trash2,
  RefreshCw,
  Plus,
  Check,
  X,
  AlertCircle,
} from "lucide-react";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "./ui/dialog";
import { Button } from "./ui/button";
import { Input } from "./ui/input";
import { Switch } from "./ui/switch";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "./ui/tabs";
import { Label } from "./ui/label";
import type { BlockListEntry } from "../lib/types";
import { GetNodeService } from "@/IpcService/GetNodeService";

interface BlockedPeersDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function BlockedPeersDialog({
  open,
  onOpenChange,
}: BlockedPeersDialogProps) {
  const [blockList, setBlockList] = useState<BlockListEntry[]>([]);
  const [loading, setLoading] = useState(false);
  const [newAddress, setNewAddress] = useState("");
  const [isWhitelist, setIsWhitelist] = useState(false);
  const [validationError, setValidationError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState("all");
  const [refreshTrigger, setRefreshTrigger] = useState(0);

  // Fetch block list entries when the dialog opens or refresh is triggered
  useEffect(() => {
    if (open) {
      fetchBlockList();
    }
  }, [open, refreshTrigger]);

  const fetchBlockList = async () => {
    setLoading(true);
    try {
      const nodeService = GetNodeService();
      const blockListResponse = await nodeService.GetBlockList();

      // Convert to our frontend type
      const entries: BlockListEntry[] = (blockListResponse.entries || []).map(
        (entry) => ({
          url: entry.url,
          inWhitelist: entry.inWhitelist,

        })

      );

      setBlockList(entries);
    } catch (error) {
      console.error("Failed to fetch block list:", error);
    } finally {
      setLoading(false);
    }
  };

  const handleRefresh = () => {
    setRefreshTrigger((prev) => prev + 1);
  };

  const validateIPAddress = (address: string): boolean => {
    try {
      // Check if it's a valid IPv4 address or CIDR
      if (address.includes(".")) {
        // Check if it's a CIDR notation
        if (address.includes("/")) {
          const [ip, prefix] = address.split("/");
          const prefixNum = Number.parseInt(prefix, 10);
          // This will throw if invalid
          new Address4(ip);
          return !isNaN(prefixNum) && prefixNum >= 0 && prefixNum <= 32;
        }
        // This will throw if invalid
        new Address4(address);
        return true;
      }

      // Check if it's a valid IPv6 address or CIDR
      if (address.includes(":")) {
        // Check if it's a CIDR notation
        if (address.includes("/")) {
          const [ip, prefix] = address.split("/");
          const prefixNum = Number.parseInt(prefix, 10);
          // This will throw if invalid
          new Address6(ip);
          return !isNaN(prefixNum) && prefixNum >= 0 && prefixNum <= 128;
        }
        // This will throw if invalid
        new Address6(address);
        return true;
      }

      return false;
    } catch (error) {
      return false;
    }
  };

  const handleAddEntry = async () => {
    if (!newAddress.trim()) {
      setValidationError("Address cannot be empty");
      return;
    }

    if (!validateIPAddress(newAddress)) {
      setValidationError("Invalid IP address or CIDR range");
      return;
    }

    setValidationError(null);
    setLoading(true);

    try {
      const nodeService = GetNodeService();
      await nodeService.ModifyBlockListEntry({
        url: newAddress,
        inWhitelist: isWhitelist,
        shouldRemove: false,
      });
      setNewAddress("");
      handleRefresh();
    } catch (error) {
      console.error("Failed to add block list entry:", error);
      setValidationError("Failed to add entry. Please try again.");
    } finally {
      setLoading(false);
    }
  };

  const handleRemoveEntry = async (entry: BlockListEntry) => {
    setLoading(true);
    try {
      const nodeService = GetNodeService();
      await nodeService.ModifyBlockListEntry({
        url: entry.url,
        inWhitelist: entry.inWhitelist,
        shouldRemove: true,
      });
      handleRefresh();
    } catch (error) {
      console.error("Failed to remove block list entry:", error);
    } finally {
      setLoading(false);
    }
  };

  const handleToggleWhitelist = async (entry: BlockListEntry) => {
    setLoading(true);
    try {
      const nodeService = GetNodeService();
      // Remove the old entry
      await nodeService.ModifyBlockListEntry({
        url: entry.url,
        inWhitelist: entry.inWhitelist,
        shouldRemove: true,
      });
      // Add the new entry with toggled whitelist status
      await nodeService.ModifyBlockListEntry({
        url: entry.url,
        inWhitelist: !entry.inWhitelist,
        shouldRemove: false,
      });
      handleRefresh();
    } catch (error) {
      console.error("Failed to toggle whitelist status:", error);
    } finally {
      setLoading(false);
    }
  };

  const filteredBlockList = blockList.filter((entry) => {
    if (activeTab === "all") return true;
    if (activeTab === "whitelist") return entry.inWhitelist;
    if (activeTab === "blacklist") return !entry.inWhitelist;
    return true;
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[550px]">
        <DialogHeader>
          <DialogTitle className="flex items-center">
            <Shield className="mr-2 h-5 w-5" />
            Manage Blocked Peers
          </DialogTitle>
          <DialogDescription>
            Add, remove, or modify IP addresses and CIDR ranges in your block
            list.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-2">
          {/* Add new entry form */}
          <div className="space-y-2">
            <Label htmlFor="new-address">Add new address</Label>
            <div className="flex items-center space-x-2">
              <Input
                id="new-address"
                placeholder="Enter IP address or CIDR range"
                value={newAddress}
                onChange={(e) => setNewAddress(e.target.value)}
                className="flex-1"
                disabled={loading}
              />
              <div className="flex items-center space-x-2">
                <Switch
                  id="whitelist-toggle"
                  checked={isWhitelist}
                  onCheckedChange={setIsWhitelist}
                  disabled={loading}
                />
                <Label htmlFor="whitelist-toggle" className="text-sm">
                  Whitelist
                </Label>
              </div>
              <Button
                size="sm"
                onClick={handleAddEntry}
                disabled={loading || !newAddress.trim()}
              >
                <Plus className="h-4 w-4 mr-1" />
                Add
              </Button>
            </div>
            {validationError && (
              <div className="text-sm text-red-500 flex items-center mt-1">
                <AlertCircle className="h-4 w-4 mr-1" />
                {validationError}
              </div>
            )}
          </div>

          {/* Tabs for filtering */}
          <Tabs
            defaultValue="all"
            value={activeTab}
            onValueChange={setActiveTab}
          >
            <div className="flex items-center justify-between">
              <TabsList>
                <TabsTrigger value="all">All</TabsTrigger>
                <TabsTrigger value="whitelist">Whitelist</TabsTrigger>
                <TabsTrigger value="blacklist">Blacklist</TabsTrigger>
              </TabsList>
              <Button
                variant="outline"
                size="sm"
                onClick={handleRefresh}
                disabled={loading}
              >
                <RefreshCw
                  className={`h-4 w-4 mr-1 ${loading ? "animate-spin" : ""}`}
                />
                Refresh
              </Button>
            </div>

            <TabsContent value="all" className="space-y-4">
              {renderBlockList(filteredBlockList)}
            </TabsContent>
            <TabsContent value="whitelist" className="space-y-4">
              {renderBlockList(filteredBlockList)}
            </TabsContent>
            <TabsContent value="blacklist" className="space-y-4">
              {renderBlockList(filteredBlockList)}
            </TabsContent>
          </Tabs>
        </div>
      </DialogContent>
    </Dialog>
  );

  function renderBlockList(entries: BlockListEntry[]) {
    if (loading && entries.length === 0) {
      return (
        <div className="text-center py-8">
          <RefreshCw className="h-8 w-8 animate-spin mx-auto text-muted-foreground" />
          <p className="mt-2 text-muted-foreground">Loading block list...</p>
        </div>
      );
    }

    if (entries.length === 0) {
      return (
        <div className="text-center py-8 text-muted-foreground">
          <p>No entries found.</p>
        </div>
      );
    }

    return (
      <div className="border rounded-md">
        <div className="grid grid-cols-[1fr,100px,80px] gap-2 p-3 bg-muted font-medium text-sm">
          <div>Address</div>
          <div className="text-center">Type</div>
          <div className="text-center">Actions</div>
        </div>
        <div className="divide-y">
          {entries.map((entry) => (
            <div
              key={`${entry.url}-${entry.inWhitelist}`}
              className="grid grid-cols-[1fr,100px,80px] gap-2 p-3 items-center"
            >
              <div className="font-mono text-sm">{entry.url}</div>
              <div className="text-center">
                {entry.inWhitelist ? (
                  <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-green-100 text-green-800">
                    <Check className="h-3 w-3 mr-1" />
                    Allow
                  </span>
                ) : (
                  <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-red-100 text-red-800">
                    <X className="h-3 w-3 mr-1" />
                    Block
                  </span>
                )}
              </div>
              <div className="flex justify-center space-x-1">
                <Button
                  variant="ghost"
                  size="icon"
                  onClick={() => handleToggleWhitelist(entry)}
                  disabled={loading}
                  title={
                    entry.inWhitelist
                      ? "Move to blacklist"
                      : "Move to whitelist"
                  }
                >
                  {entry.inWhitelist ? (
                    <X className="h-4 w-4 text-red-500" />
                  ) : (
                    <Check className="h-4 w-4 text-green-500" />
                  )}
                </Button>
                <Button
                  variant="ghost"
                  size="icon"
                  onClick={() => handleRemoveEntry(entry)}
                  disabled={loading}
                  title="Remove entry"
                >
                  <Trash2 className="h-4 w-4 text-muted-foreground hover:text-red-500" />
                </Button>
              </div>
            </div>
          ))}
        </div>
      </div>
    );
  }
}
