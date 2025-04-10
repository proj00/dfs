import { GetNodeService } from "@/IpcService/NodeServiceClient";
import { type ClassValue, clsx } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export function formatFileSize(bytes: number): string {
  if (bytes === 0) return "0 Bytes";

  const k = 1024;
  const sizes = ["Bytes", "KB", "MB", "GB", "TB"];

  const i = Math.floor(Math.log(bytes) / Math.log(k));

  return (
    Number.parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + " " + sizes[i]
  );
}

export function formatDate(dateString: string): string {
  const date = new Date(dateString);
  const now = new Date();

  // Check if it's today
  if (date.toDateString() === now.toDateString()) {
    return (
      "Today, " +
      date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })
    );
  }

  // Check if it's yesterday
  const yesterday = new Date(now);
  yesterday.setDate(now.getDate() - 1);
  if (date.toDateString() === yesterday.toDateString()) {
    return (
      "Yesterday, " +
      date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })
    );
  }

  // Otherwise, return the date
  return date.toLocaleDateString([], {
    month: "short",
    day: "numeric",
    year: date.getFullYear() !== now.getFullYear() ? "numeric" : undefined,
  });
}

export function toBase64(array: Uint8Array): string {
  return btoa(String.fromCharCode(...array));
}

export async function copyToClipboard(str: string): Promise<void> {
  const service = await GetNodeService();
  await service.CopyToClipboard(str);
}
