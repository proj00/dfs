import "@testing-library/jest-dom";
import { render, screen, fireEvent, waitFor, act } from "@testing-library/react";
import { jest } from "@jest/globals";
import { GetNodeService } from "@/IpcService/GetNodeService";
import { BlockedPeersDialog } from "@/components/blocked-peers-dialog";
import { NodeServiceClient } from "@/types/wrap/NodeServiceClient";

// Mock the GetNodeService function
jest.mock("@/IpcService/GetNodeService", () => ({
  GetNodeService: jest.fn(
    () =>
      ({
        GetBlockList: jest.fn().mockResolvedValue({
          // Make sure this matches the structure your component expects for 'entries'
          entries: [ // Changed from BlockListEntries to entries based on component code
            { url: "192.168.1.1", inWhitelist: false },
            { url: "10.0.0.0/24", inWhitelist: false },
            { url: "2001:db8::/64", inWhitelist: true },
          ],
        } as never),
        ModifyBlockListEntry: jest.fn().mockResolvedValue(undefined as never), // Simplified mockResolvedValue
      } as unknown as NodeServiceClient)
  ),
}));

describe("BlockedPeersDialog", () => {
  let mockNodeService: NodeServiceClient;

  beforeEach(async () => { // Make beforeEach async if GetNodeService is async
    jest.clearAllMocks();
    // Get the mocked service instance to allow for more specific assertions if needed
    mockNodeService = GetNodeService();
  });

  it("renders the dialog when open is true", async () => {
    await act(async () => {
      render(<BlockedPeersDialog open={true} onOpenChange={() => {}} />);
    });
    expect(screen.getByText("Manage Blocked Peers")).toBeInTheDocument();
  });

  it("does not render the dialog when open is false",async () => {
    await act(async () => {
      render(<BlockedPeersDialog open={false} onOpenChange={() => {}} />);
    });
    expect(screen.queryByText("Manage Blocked Peers")).not.toBeInTheDocument();
  });

  it("displays block list entries when loaded", async () => {
    render(<BlockedPeersDialog open={true} onOpenChange={() => {}} />);

    await waitFor(() => {
      expect(screen.getByText("192.168.1.1")).toBeInTheDocument();
      expect(screen.getByText("10.0.0.0/24")).toBeInTheDocument();
      expect(screen.getByText("2001:db8::/64")).toBeInTheDocument();
    });

    // Verify GetNodeService was called
    expect(GetNodeService).toHaveBeenCalled();
  });

  it("validates IP address input", async () => {
    await act(async () => {
      render(<BlockedPeersDialog open={true} onOpenChange={() => {}} />);
    });

    // Invalid IP
    const input = screen.getByPlaceholderText("Enter IP address or CIDR range");
    await act(async () => {
      fireEvent.change(input, { target: { value: "invalid-ip" } });
      fireEvent.click(screen.getByText("Add"));  
    });

    expect(
      screen.getByText("Invalid IP address or CIDR range")
    ).toBeInTheDocument();
    await act(async () => {
      
      fireEvent.change(input, { target: { value: "192.168.1.5" } });
      fireEvent.click(screen.getByText("Add"));
    });
    // Valid IPv4

    // Verify ModifyBlockListEntry was called with correct parameters
    const nodeService = await GetNodeService();
    expect(nodeService.ModifyBlockListEntry).toHaveBeenCalledWith({
      address: "192.168.1.5",
      inWhitelist: false,
      shouldRemove: false,
    });
  });

  it("filters entries based on tab selection", async () => {
    await act(async () => {
      render(<BlockedPeersDialog open={true} onOpenChange={() => {}} />);
    });

    await waitFor(() => {
      expect(screen.getByText("192.168.1.1")).toBeInTheDocument();
      expect(screen.getByText("2001:db8::/64")).toBeInTheDocument();
    });
    await act(async () => {
      
      // Switch to whitelist tab
      fireEvent.click(screen.getByRole("tab", { name: "Whitelist" }));
    });

    await waitFor(() => {
      expect(screen.queryByText("192.168.1.1")).not.toBeInTheDocument();
      expect(screen.getByText("2001:db8::/64")).toBeInTheDocument();
    });
    await act(async () => {  
      // Switch to blacklist tab
      fireEvent.click(screen.getByRole("tab", { name: "Blacklist" }));
    });

    await waitFor(() => {
      expect(screen.getByText("192.168.1.1")).toBeInTheDocument();
      expect(screen.queryByText("2001:db8::/64")).not.toBeInTheDocument();
    });
  });

  it("handles removing an entry", async () => {
    await act(async () => {
      render(<BlockedPeersDialog open={true} onOpenChange={() => {}} />);
    });

    await waitFor(() => {
      expect(screen.getByText("192.168.1.1")).toBeInTheDocument();
    });

    // Find and click the delete button for the first entry
    const deleteButtons = screen.getAllByTitle("Remove entry");
    await act(async () => {
      fireEvent.click(deleteButtons[0]);
    });

    // Verify ModifyBlockListEntry was called with correct parameters
    const nodeService = await GetNodeService();
    expect(nodeService.ModifyBlockListEntry).toHaveBeenCalledWith({
      address: "192.168.1.1",
      inWhitelist: false,
      shouldRemove: true,
    });
  });


  //doesnt acctualy add the ip
  it("handles toggling whitelist status", async () => {
    await act(async () => {
      render(<BlockedPeersDialog open={true} onOpenChange={() => {}} />);
    });

    await waitFor(() => {
      expect(screen.getByText("192.168.1.1")).toBeInTheDocument();
    });

    // Find and click the toggle button for the first entry (blacklist to whitelist)
    const toggleButtons = screen.getAllByTitle("Move to whitelist");
    await act(async () => {
      fireEvent.click(toggleButtons[0]);
    });

    // Verify ModifyBlockListEntry was called with correct parameters
    const nodeService = await GetNodeService();
    expect(nodeService.ModifyBlockListEntry).toHaveBeenCalledWith({
      address: "192.168.1.1",
      inWhitelist: false,
      shouldRemove: true,
    });
    expect(nodeService.ModifyBlockListEntry).toHaveBeenCalledWith({
      address: "192.168.1.1",
      inWhitelist: true,
      shouldRemove: false,
    });
    
  });
  //===================
  it("validates IP address input and adds a valid IP to the blacklist", async () => {
    await act(async () => {
      render(<BlockedPeersDialog open={true} onOpenChange={() => {}} />);
    });

    const input = screen.getByPlaceholderText("Enter IP address or CIDR range");
    const addButton = screen.getByRole("button", { name: /add/i }); // More resilient selector

    // Test 1: Attempt to add an invalid IP
    await act(async () => {
      fireEvent.change(input, { target: { value: "invalid-ip" } });
      fireEvent.click(addButton);
    });

    expect(await screen.findByText("Invalid IP address or CIDR range")).toBeInTheDocument();
    expect(mockNodeService.ModifyBlockListEntry).not.toHaveBeenCalled();

    // Test 2: Add a valid IPv4 address to blacklist (default)
    const validIpToAdd = "192.168.1.5";
    await act(async () => {
      fireEvent.change(input, { target: { value: validIpToAdd } });
      // Ensure validation message is gone if it was previously visible
      expect(screen.queryByText("Invalid IP address or CIDR range")).not.toBeInTheDocument();
      fireEvent.click(addButton);
    });

    // Wait for the async operation to complete
    await waitFor(() => {
      expect(mockNodeService.ModifyBlockListEntry).toHaveBeenCalledWith({
        url: validIpToAdd,        // Corrected from 'address' to 'url'
        inWhitelist: false,     // Default is blacklist
        shouldRemove: false,
      });
    });
  });
});

