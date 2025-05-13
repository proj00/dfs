import "@testing-library/jest-dom";
import { render, screen, fireEvent, waitFor, act } from "@testing-library/react";
import { jest } from "@jest/globals";
//import { GetNodeService } from "@/IpcService/GetNodeService"; // Ensure path is correct
import { BlockedPeersDialog } from "@/components/blocked-peers-dialog"; // Ensure path is correct
import { NodeServiceClient } from "@/types/wrap/NodeServiceClient"; // Ensure path is correct

/**
 * @jest-environment jsdom
 */

// Define mock functions that can be easily managed per test
const mockGetBlockList = jest.fn();
const mockModifyBlockListEntry = jest.fn();

jest.mock("@/IpcService/GetNodeService", () => ({
  GetNodeService: jest.fn(() => ({
    GetBlockList: mockGetBlockList,
    ModifyBlockListEntry: mockModifyBlockListEntry,
  }) as unknown as NodeServiceClient),
}));

describe("BlockedPeersDialog", () => {
  beforeEach(() => {
    jest.clearAllMocks(); // Clears usage data and mock implementations

    // Default mock implementation for GetBlockList for most tests
    mockGetBlockList.mockResolvedValue({
      entries: [
        { url: "192.168.1.1", inWhitelist: false },
        { url: "10.0.0.0/24", inWhitelist: false },
        { url: "2001:db8::/64", inWhitelist: true },
      ],
    }as never);

    // Default mock implementation for ModifyBlockListEntry
    mockModifyBlockListEntry.mockResolvedValue(undefined as never);
  });

  it("renders the dialog when open is true and loads initial entries", async () => {
    render(<BlockedPeersDialog open={true} onOpenChange={() => {}} />);

    expect(screen.getByText("Manage Blocked Peers")).toBeInTheDocument();

    await waitFor(() => {
      expect(mockGetBlockList).toHaveBeenCalledTimes(1);
      // Check for entries based on the "All" tab default view
      expect(screen.getByText("192.168.1.1")).toBeInTheDocument();
      expect(screen.getByText("10.0.0.0/24")).toBeInTheDocument();
      expect(screen.getByText("2001:db8::/64")).toBeInTheDocument();
    });
  });

  it("does not render the dialog when open is false", () => {
    render(<BlockedPeersDialog open={false} onOpenChange={() => {}} />);
    expect(screen.queryByText("Manage Blocked Peers")).not.toBeInTheDocument();
  });

  it("validates IP address input and adds a valid IP to the blacklist", async () => {
    render(<BlockedPeersDialog open={true} onOpenChange={() => {}} />);
    // Wait for initial list to load
    await waitFor(() => expect(mockGetBlockList).toHaveBeenCalledTimes(1));

    const input = screen.getByPlaceholderText("Enter IP address or CIDR range");
    const addButton = screen.getByRole("button", { name: /add/i });

    // Test 1: Attempt to add an invalid IP
    await act(async () => {
      fireEvent.change(input, { target: { value: "invalid-ip" } });
      fireEvent.click(addButton);
    });

    expect(await screen.findByText("Invalid IP address or CIDR range")).toBeInTheDocument();
    expect(mockModifyBlockListEntry).not.toHaveBeenCalled();

    // Test 2: Add a valid IPv4 address to blacklist (default state for the switch)
    const validIpToAdd = "192.168.1.5";
    await act(async () => {
      fireEvent.change(input, { target: { value: validIpToAdd } });
      fireEvent.click(addButton);
    });

    await waitFor(() => {
      expect(screen.queryByText("Invalid IP address or CIDR range")).not.toBeInTheDocument();
      expect(mockModifyBlockListEntry).toHaveBeenCalledWith({
        url: validIpToAdd,
        inWhitelist: false, // Default is blacklist
        shouldRemove: false,
      });
    });

    expect(input).toHaveValue(""); // Input should be cleared
    // GetBlockList should be called again to refresh the list
    expect(mockGetBlockList).toHaveBeenCalledTimes(2); // Initial load + refresh after add
  });

  it("adds a valid IP to the whitelist when toggle is active", async () => {
    render(<BlockedPeersDialog open={true} onOpenChange={() => {}} />);
    await waitFor(() => expect(mockGetBlockList).toHaveBeenCalledTimes(1));

    const input = screen.getByPlaceholderText("Enter IP address or CIDR range");
    const addButton = screen.getByRole("button", { name: /add/i });
    // Find the label associated with the switch; clicking the label should toggle it.
    const whitelistLabel = screen.getByText("Whitelist"); // Assumes "Whitelist" is the text of the <Label>

    const validIpToAdd = "2001:db8::a";
    await act(async () => {
      fireEvent.click(whitelistLabel); // Click the label to toggle the switch to 'on' (whitelist)
      fireEvent.change(input, { target: { value: validIpToAdd } });
      fireEvent.click(addButton);
    });

    await waitFor(() => {
      expect(mockModifyBlockListEntry).toHaveBeenCalledWith({
        url: validIpToAdd,
        inWhitelist: true, // Switched to whitelist
        shouldRemove: false,
      });
    });
    expect(input).toHaveValue("");
    expect(mockGetBlockList).toHaveBeenCalledTimes(2); // Initial load + refresh after add
  });

  it("filters entries based on tab selection", async () => {
    render(<BlockedPeersDialog open={true} onOpenChange={() => {}} />);
    await waitFor(() => { // Ensure initial "All" tab content is loaded
        expect(screen.getByText("192.168.1.1")).toBeInTheDocument();
        expect(screen.getByText("2001:db8::/64")).toBeInTheDocument();
    });

    // Switch to whitelist tab
    const whitelistTab = screen.getByRole("tab", { name: "Whitelist" });
    await act(async () => { fireEvent.click(whitelistTab); });

    await waitFor(() => {
      expect(screen.queryByText("192.168.1.1")).not.toBeInTheDocument();
      expect(screen.queryByText("10.0.0.0/24")).not.toBeInTheDocument();
      expect(screen.getByText("2001:db8::/64")).toBeInTheDocument();
    });

    // Switch to blacklist tab
    const blacklistTab = screen.getByRole("tab", { name: "Blacklist" });
    await act(async () => { fireEvent.click(blacklistTab); });

    await waitFor(() => {
      expect(screen.getByText("192.168.1.1")).toBeInTheDocument();
      expect(screen.getByText("10.0.0.0/24")).toBeInTheDocument();
      expect(screen.queryByText("2001:db8::/64")).not.toBeInTheDocument();
    });

    // Switch back to All tab
    const allTab = screen.getByRole("tab", { name: "All" });
    await act(async () => { fireEvent.click(allTab); });

    await waitFor(() => {
      expect(screen.getByText("192.168.1.1")).toBeInTheDocument();
      expect(screen.getByText("10.0.0.0/24")).toBeInTheDocument();
      expect(screen.getByText("2001:db8::/64")).toBeInTheDocument();
    });
  });

  it("handles removing an entry", async () => {
    render(<BlockedPeersDialog open={true} onOpenChange={() => {}} />);
    await waitFor(() => expect(screen.getByText("192.168.1.1")).toBeInTheDocument());

    // Find the row for "192.168.1.1" and click its delete button
    const entryRow = screen.getByText("192.168.1.1").closest("div[class*='grid-cols']"); // More generic selector for the row
    const deleteButton = entryRow?.querySelector('button[title="Remove entry"]');
    expect(deleteButton).toBeInTheDocument(); // Ensure button is found

    if(deleteButton) { // Type guard
        await act(async () => { fireEvent.click(deleteButton); });
    }


    await waitFor(() => {
      expect(mockModifyBlockListEntry).toHaveBeenCalledWith({
        url: "192.168.1.1",
        inWhitelist: false, // It was initially blacklisted
        shouldRemove: true,
      });
    });
    expect(mockGetBlockList).toHaveBeenCalledTimes(2); // Initial load + refresh
  });

  it("handles toggling an entry from blacklist to whitelist", async () => {
    render(<BlockedPeersDialog open={true} onOpenChange={() => {}} />);
    await waitFor(() => expect(screen.getByText("192.168.1.1")).toBeInTheDocument()); // Initially blacklisted

    const entryRow = screen.getByText("192.168.1.1").closest("div[class*='grid-cols']");
    const toggleButton = entryRow?.querySelector('button[title="Move to whitelist"]'); // Title when it's blacklisted
    expect(toggleButton).toBeInTheDocument();

    if(toggleButton) { //Type guard
        await act(async () => { fireEvent.click(toggleButton); });
    }


    await waitFor(() => {
      // Component logic: removes old entry, then adds new (toggled) entry
      expect(mockModifyBlockListEntry).toHaveBeenCalledWith({
        url: "192.168.1.1",
        inWhitelist: false, // Original state
        shouldRemove: true,  // First call: remove the old entry
      });
      expect(mockModifyBlockListEntry).toHaveBeenCalledWith({
        url: "192.168.1.1",
        inWhitelist: true,  // New state (toggled to whitelist)
        shouldRemove: false, // Second call: add the new entry
      });
    });
    expect(mockGetBlockList).toHaveBeenCalledTimes(2); // Initial load + refresh
  });
});